using System.Runtime.InteropServices;

namespace Slate.Core.Storage;

public enum BlockType : byte
{
    Invalid = 0,
    TableMetadata = 1,
    ColumnDataChunk = 2,
    DictionaryPage = 3,
    IndexNode = 4,
    WalFrame = 5,
    ZoneMapPage = 6
}

public enum CompressionCodec : byte
{
    None = 0,
    RunLength = 1,
    BitPacked = 2,
    Dictionary = 3,
    Gorilla = 4,
    Lz4 = 5
}

/// <summary>
/// A 64-byte cache-line aligned binary header prefixed to every persistent Slate page,
/// SSTable block, and transaction journal frame.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
public struct BlockHeader
{
    public const uint MagicNumber = 0x54414C53; // 'SLAT' in little-endian
    public const ushort CurrentVersion = 1;

    public uint Magic;              // 4 bytes: 'SLAT'
    public ushort Version;          // 2 bytes: format version
    public BlockType Type;          // 1 byte: block kind
    public CompressionCodec Codec;  // 1 byte: encoding/compression
    public uint UncompressedBytes;  // 4 bytes: raw logical payload size
    public uint CompressedBytes;    // 4 bytes: wire storage payload size
    public uint RowCount;           // 4 bytes: number of records/elements
    public uint ChecksumCrc32C;     // 4 bytes: Castagnoli CRC32C over payload
    public ulong Lsn;               // 8 bytes: Log Sequence Number
    public ulong TimestampUtc;      // 8 bytes: Unix microsecond creation timestamp

    // 24 bytes of reserved alignment padding to reach exactly 64 bytes
    private unsafe fixed byte _padding[24];

    public static BlockHeader Create(
        BlockType type,
        CompressionCodec codec,
        uint uncompressedBytes,
        uint compressedBytes,
        uint rowCount,
        uint crc32c,
        ulong lsn = 0,
        ulong timestampUtc = 0)
    {
        var header = new BlockHeader
        {
            Magic = MagicNumber,
            Version = CurrentVersion,
            Type = type,
            Codec = codec,
            UncompressedBytes = uncompressedBytes,
            CompressedBytes = compressedBytes,
            RowCount = rowCount,
            ChecksumCrc32C = crc32c,
            Lsn = lsn,
            TimestampUtc = timestampUtc == 0 ? (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000 : timestampUtc
        };
        return header;
    }

    public readonly bool IsValid() => Magic == MagicNumber && Version == CurrentVersion;

    public readonly byte[] ToByteArray()
    {
        var buffer = new byte[64];
        MemoryMarshal.Write(buffer, in this);
        return buffer;
    }

    public static BlockHeader FromByteArray(ReadOnlySpan<byte> span)
    {
        if (span.Length < 64)
            throw new ArgumentException("Buffer must contain at least 64 bytes for BlockHeader.", nameof(span));
        return MemoryMarshal.Read<BlockHeader>(span);
    }
}
