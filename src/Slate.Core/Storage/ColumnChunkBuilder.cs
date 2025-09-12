using Slate.Core.Primitives;

namespace Slate.Core.Storage;

/// <summary>
/// Analyzes uncompressed ColumnVector batches, evaluates cardinality and repetition heuristics,
/// applies the optimal columnar compression codec, and builds a verified ColumnChunk.
/// </summary>
public static class ColumnChunkBuilder
{
    public static ColumnChunk Build(int columnOrdinal, ColumnVector vector)
    {
        var zoneMap = new ZoneMap();
        int rowCount = vector.Length;

        for (int i = 0; i < rowCount; i++)
        {
            zoneMap.Observe(vector.GetValue(i));
        }

        if (rowCount == 0)
        {
            return new ColumnChunk(columnOrdinal, vector.Type, CompressionCodec.None, 0, zoneMap, Array.Empty<byte>(), 0);
        }

        CompressionCodec codec;
        byte[] payload;

        switch (vector.Type)
        {
            case DataType.Boolean:
            case DataType.Int8:
            case DataType.Int16:
            case DataType.Int32:
            case DataType.Int64:
            case DataType.TimestampUtc:
                var intValues = new long[rowCount];
                int runChanges = 0;
                long prev = 0;
                for (int i = 0; i < rowCount; i++)
                {
                    intValues[i] = vector.GetInt64(i);
                    if (i > 0 && intValues[i] != prev) runChanges++;
                    prev = intValues[i];
                }

                // If run count is less than 30% of total rows, RLE is vastly superior
                if (runChanges < rowCount * 0.3)
                {
                    codec = CompressionCodec.RunLength;
                    payload = RleCodec.EncodeInt64(intValues);
                }
                else
                {
                    codec = CompressionCodec.BitPacked;
                    payload = BitPackedCodec.EncodeInt64(intValues);
                }
                break;

            case DataType.Float32:
            case DataType.Float64:
                var floatValues = new double[rowCount];
                for (int i = 0; i < rowCount; i++)
                {
                    floatValues[i] = vector.GetDouble(i);
                }
                codec = CompressionCodec.Gorilla;
                payload = GorillaCodec.EncodeFloat64(floatValues);
                break;

            case DataType.String:
                var stringValues = new string?[rowCount];
                var uniqueStrings = new HashSet<string?>(StringComparer.Ordinal);
                int stringRunChanges = 0;
                string? prevStr = null;

                for (int i = 0; i < rowCount; i++)
                {
                    stringValues[i] = vector.GetString(i);
                    uniqueStrings.Add(stringValues[i]);
                    if (i > 0 && !string.Equals(stringValues[i], prevStr, StringComparison.Ordinal))
                    {
                        stringRunChanges++;
                    }
                    prevStr = stringValues[i];
                }

                if (stringRunChanges < rowCount * 0.25)
                {
                    codec = CompressionCodec.RunLength;
                    payload = RleCodec.EncodeString(stringValues);
                }
                else
                {
                    codec = CompressionCodec.Dictionary;
                    payload = DictionaryCodec.EncodeStrings(stringValues);
                }
                break;

            case DataType.VectorFloat32:
            case DataType.Binary:
            default:
                codec = CompressionCodec.None;
                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    for (int i = 0; i < rowCount; i++)
                    {
                        if (vector.IsNull(i))
                        {
                            writer.Write(true);
                        }
                        else
                        {
                            writer.Write(false);
                            if (vector.Type == DataType.VectorFloat32)
                            {
                                float[]? vec = vector.GetVector(i);
                                writer.Write(vec?.Length ?? 0);
                                if (vec is not null)
                                {
                                    for (int d = 0; d < vec.Length; d++) writer.Write(vec[d]);
                                }
                            }
                            else
                            {
                                byte[] bin = vector.GetValue(i).AsBinary();
                                writer.Write(bin.Length);
                                writer.Write(bin);
                            }
                        }
                    }
                    payload = ms.ToArray();
                }
                break;
        }

        uint crc = Crc32C.Compute(payload);
        return new ColumnChunk(columnOrdinal, vector.Type, codec, rowCount, zoneMap, payload, crc);
    }
}
