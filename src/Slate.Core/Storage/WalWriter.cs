namespace Slate.Core.Storage;

/// <summary>
/// Thread-safe, append-only Write-Ahead Log (WAL) writer with double-length framing and CRC32C integrity.
/// </summary>
public sealed class WalWriter : IDisposable
{
    private readonly Stream _stream;
    private readonly BinaryWriter _writer;
    private readonly object _lock = new();
    private ulong _currentLsn;

    public ulong CurrentLsn => _currentLsn;

    public WalWriter(Stream stream, ulong initialLsn = 0)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _writer = new BinaryWriter(_stream, System.Text.Encoding.UTF8, leaveOpen: true);
        _currentLsn = initialLsn;
    }

    public ulong Append(ulong txId, WalRecordType recordType, string tableName, Slate.Core.Primitives.SlateValue pk, Slate.Core.Primitives.SlateValue[] values)
    {
        lock (_lock)
        {
            _currentLsn++;
            var record = new WalRecord(_currentLsn, txId, recordType, tableName, pk, values);
            byte[] payload = record.Serialize();
            uint crc = Crc32C.Compute(payload);

            var header = BlockHeader.Create(
                type: BlockType.WalFrame,
                codec: CompressionCodec.None,
                uncompressedBytes: (uint)payload.Length,
                compressedBytes: (uint)payload.Length,
                rowCount: 1,
                crc32c: crc,
                lsn: _currentLsn
            );

            byte[] headerBytes = header.ToByteArray();
            uint frameLen = (uint)(headerBytes.Length + payload.Length);

            // Double framing: [frameLen, header, payload, frameLen]
            _writer.Write(frameLen);
            _writer.Write(headerBytes);
            _writer.Write(payload);
            _writer.Write(frameLen);
            _writer.Flush();

            return _currentLsn;
        }
    }

    public void Flush()
    {
        lock (_lock)
        {
            _writer.Flush();
            _stream.Flush();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer.Dispose();
        }
    }
}

/// <summary>
/// Sequential forward reader for Write-Ahead Log frames with crash corruption detection.
/// </summary>
public sealed class WalReader
{
    public static List<WalRecord> ReadAll(Stream stream, out bool cleanEof)
    {
        var records = new List<WalRecord>();
        cleanEof = true;

        if (stream.Length == 0)
            return records;

        stream.Position = 0;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);

        while (stream.Position < stream.Length)
        {
            long startPos = stream.Position;
            if (stream.Length - startPos < 4 + 64 + 4)
            {
                // Truncated trailing bytes from incomplete write
                cleanEof = false;
                break;
            }

            uint frameLen = reader.ReadUInt32();
            if (frameLen < 64 || (long)frameLen > (stream.Length - startPos))
            {
                cleanEof = false;
                break;
            }

            byte[] headerBytes = reader.ReadBytes(64);
            var header = BlockHeader.FromByteArray(headerBytes);
            if (!header.IsValid() || header.Type != BlockType.WalFrame)
            {
                cleanEof = false;
                break;
            }

            int payloadLen = (int)(frameLen - 64);
            byte[] payload = reader.ReadBytes(payloadLen);
            uint trailingLen = reader.ReadUInt32();

            if (trailingLen != frameLen)
            {
                cleanEof = false;
                break;
            }

            uint computedCrc = Crc32C.Compute(payload);
            if (computedCrc != header.ChecksumCrc32C)
            {
                // Bit rot or incomplete disk flush
                cleanEof = false;
                break;
            }

            var record = WalRecord.Deserialize(payload);
            records.Add(record);
        }

        return records;
    }
}
