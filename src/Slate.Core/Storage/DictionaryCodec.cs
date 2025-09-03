using System.Buffers.Binary;
using System.Text;

namespace Slate.Core.Storage;

/// <summary>
/// Dictionary encoding codec for string and categorical columns.
/// Maps distinct values to compact integer surrogate keys and compresses indices using Frame-of-Reference bit-packing.
/// </summary>
public static class DictionaryCodec
{
    public static byte[] EncodeStrings(IReadOnlyList<string?> values)
    {
        if (values.Count == 0)
            return Array.Empty<byte>();

        // Build sorted unique dictionary entries
        var uniqueMap = new Dictionary<string, int>(StringComparer.Ordinal);
        var dictionaryList = new List<string>();
        bool hasNull = false;

        foreach (var val in values)
        {
            if (val is null)
            {
                hasNull = true;
            }
            else if (!uniqueMap.ContainsKey(val))
            {
                uniqueMap[val] = dictionaryList.Count;
                dictionaryList.Add(val);
            }
        }

        // Map values to indices: -1 for null, >= 0 for dictionary index
        // To allow bit-packing, shift indices so null is represented as 0, and dict indices are 1..K
        var indexArray = new long[values.Count];
        for (int i = 0; i < values.Count; i++)
        {
            var val = values[i];
            if (val is null)
            {
                indexArray[i] = 0;
            }
            else
            {
                indexArray[i] = uniqueMap[val] + (hasNull ? 1 : 0);
            }
        }

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        // Header: [hasNull: 1 byte, dictCount: 4 bytes]
        writer.Write(hasNull);
        writer.Write(dictionaryList.Count);

        for (int d = 0; d < dictionaryList.Count; d++)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(dictionaryList[d]);
            writer.Write(utf8.Length);
            writer.Write(utf8);
        }

        // Encode indices with BitPackedCodec
        byte[] packedIndices = BitPackedCodec.EncodeInt64(indexArray);
        writer.Write(packedIndices.Length);
        writer.Write(packedIndices);

        return ms.ToArray();
    }

    public static string?[] DecodeStrings(ReadOnlySpan<byte> compressed)
    {
        if (compressed.IsEmpty)
            return Array.Empty<string?>();

        using var ms = new MemoryStream(compressed.ToArray());
        using var reader = new BinaryReader(ms, Encoding.UTF8);

        bool hasNull = reader.ReadBoolean();
        int dictCount = reader.ReadInt32();

        var dictionary = new string[dictCount];
        for (int d = 0; d < dictCount; d++)
        {
            int len = reader.ReadInt32();
            byte[] bytes = reader.ReadBytes(len);
            dictionary[d] = Encoding.UTF8.GetString(bytes);
        }

        int indicesByteLen = reader.ReadInt32();
        byte[] packedIndices = reader.ReadBytes(indicesByteLen);

        long[] indexArray = BitPackedCodec.DecodeInt64(packedIndices);
        var result = new string?[indexArray.Length];

        for (int i = 0; i < indexArray.Length; i++)
        {
            long idx = indexArray[i];
            if (hasNull)
            {
                if (idx == 0)
                {
                    result[i] = null;
                }
                else
                {
                    result[i] = dictionary[(int)idx - 1];
                }
            }
            else
            {
                result[i] = dictionary[(int)idx];
            }
        }

        return result;
    }
}
