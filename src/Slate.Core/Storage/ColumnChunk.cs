using Slate.Core.Primitives;

namespace Slate.Core.Storage;

/// <summary>
/// A persistent or in-memory serialized columnar chunk for a single column slice.
/// Encapsulates compressed payload bytes, compression metadata, and Zone Map bounds.
/// </summary>
public sealed class ColumnChunk
{
    public int ColumnOrdinal { get; }
    public DataType Type { get; }
    public CompressionCodec Codec { get; }
    public int RowCount { get; }
    public ZoneMap ZoneMap { get; }
    public byte[] Payload { get; }
    public uint ChecksumCrc32C { get; }

    public ColumnChunk(
        int columnOrdinal,
        DataType type,
        CompressionCodec codec,
        int rowCount,
        ZoneMap zoneMap,
        byte[] payload,
        uint checksumCrc32c)
    {
        ColumnOrdinal = columnOrdinal;
        Type = type;
        Codec = codec;
        RowCount = rowCount;
        ZoneMap = zoneMap;
        Payload = payload;
        ChecksumCrc32C = checksumCrc32c;
    }

    /// <summary>
    /// Decompresses the columnar chunk back into an active ColumnVector.
    /// </summary>
    public ColumnVector Decompress()
    {
        var vector = new ColumnVector(Type, RowCount);
        if (RowCount == 0) return vector;

        switch (Codec)
        {
            case CompressionCodec.RunLength:
                if (Type == DataType.String)
                {
                    string?[] strings = RleCodec.DecodeString(Payload);
                    for (int i = 0; i < strings.Length; i++)
                    {
                        vector.AppendString(strings[i]);
                    }
                }
                else
                {
                    long[] ints = RleCodec.DecodeInt64(Payload);
                    for (int i = 0; i < ints.Length; i++)
                    {
                        vector.AppendInt64(ints[i]);
                    }
                }
                break;

            case CompressionCodec.BitPacked:
                long[] bitPacked = BitPackedCodec.DecodeInt64(Payload);
                for (int i = 0; i < bitPacked.Length; i++)
                {
                    vector.AppendInt64(bitPacked[i]);
                }
                break;

            case CompressionCodec.Dictionary:
                string?[] dictStrings = DictionaryCodec.DecodeStrings(Payload);
                for (int i = 0; i < dictStrings.Length; i++)
                {
                    vector.AppendString(dictStrings[i]);
                }
                break;

            case CompressionCodec.Gorilla:
                double[] floats = GorillaCodec.DecodeFloat64(Payload);
                for (int i = 0; i < floats.Length; i++)
                {
                    vector.AppendDouble(floats[i]);
                }
                break;

            case CompressionCodec.None:
            default:
                // Raw fallback
                using (var ms = new MemoryStream(Payload))
                using (var reader = new BinaryReader(ms))
                {
                    for (int i = 0; i < RowCount; i++)
                    {
                        bool isNull = reader.ReadBoolean();
                        if (isNull)
                        {
                            vector.AppendNull();
                        }
                        else
                        {
                            switch (Type)
                            {
                                case DataType.VectorFloat32:
                                    int dim = reader.ReadInt32();
                                    var floatsArray = new float[dim];
                                    for (int d = 0; d < dim; d++) floatsArray[d] = reader.ReadSingle();
                                    vector.AppendVector(floatsArray);
                                    break;
                                case DataType.Binary:
                                    int len = reader.ReadInt32();
                                    byte[] bytes = reader.ReadBytes(len);
                                    vector.AppendValue(SlateValue.FromBinary(bytes));
                                    break;
                                default:
                                    vector.AppendNull();
                                    break;
                            }
                        }
                    }
                }
                break;
        }

        return vector;
    }
}
