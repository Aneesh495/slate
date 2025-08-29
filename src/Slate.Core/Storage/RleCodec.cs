using System.Buffers.Binary;
using System.Text;

namespace Slate.Core.Storage;

/// <summary>
/// Run-Length Encoding (RLE) codec for consecutive repeated values in columnar chunks.
/// Encodes runs as (count, value) pairs with 32-bit run headers.
/// </summary>
public static class RleCodec
{
    public static byte[] EncodeInt64(ReadOnlySpan<long> values)
    {
        if (values.IsEmpty)
            return Array.Empty<byte>();

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Pre-count runs
        int runCount = 0;
        long currentVal = values[0];
        int currentCount = 0;

        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == currentVal && currentCount < int.MaxValue)
            {
                currentCount++;
            }
            else
            {
                runCount++;
                currentVal = values[i];
                currentCount = 1;
            }
        }
        runCount++; // Final run

        writer.Write(runCount);
        writer.Write(values.Length);

        // Emit runs
        currentVal = values[0];
        currentCount = 0;
        for (int i = 0; i < values.Length; i++)
        {
            if (values[i] == currentVal && currentCount < int.MaxValue)
            {
                currentCount++;
            }
            else
            {
                writer.Write(currentCount);
                writer.Write(currentVal);
                currentVal = values[i];
                currentCount = 1;
            }
        }
        writer.Write(currentCount);
        writer.Write(currentVal);

        return ms.ToArray();
    }

    public static long[] DecodeInt64(ReadOnlySpan<byte> compressed)
    {
        if (compressed.IsEmpty)
            return Array.Empty<long>();

        int runCount = BinaryPrimitives.ReadInt32LittleEndian(compressed[0..4]);
        int totalValues = BinaryPrimitives.ReadInt32LittleEndian(compressed[4..8]);

        var result = new long[totalValues];
        int offset = 8;
        int writeIdx = 0;

        for (int r = 0; r < runCount; r++)
        {
            int count = BinaryPrimitives.ReadInt32LittleEndian(compressed.Slice(offset, 4));
            long val = BinaryPrimitives.ReadInt64LittleEndian(compressed.Slice(offset + 4, 8));
            offset += 12;

            for (int k = 0; k < count; k++)
            {
                result[writeIdx++] = val;
            }
        }

        return result;
    }

    public static byte[] EncodeString(IReadOnlyList<string?> values)
    {
        if (values.Count == 0)
            return Array.Empty<byte>();

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms, Encoding.UTF8);

        int runCount = 0;
        string? currentVal = values[0];
        int currentCount = 0;

        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], currentVal, StringComparison.Ordinal))
            {
                currentCount++;
            }
            else
            {
                runCount++;
                currentVal = values[i];
                currentCount = 1;
            }
        }
        runCount++;

        writer.Write(runCount);
        writer.Write(values.Count);

        currentVal = values[0];
        currentCount = 0;
        for (int i = 0; i < values.Count; i++)
        {
            if (string.Equals(values[i], currentVal, StringComparison.Ordinal))
            {
                currentCount++;
            }
            else
            {
                writer.Write(currentCount);
                WriteString(writer, currentVal);
                currentVal = values[i];
                currentCount = 1;
            }
        }
        writer.Write(currentCount);
        WriteString(writer, currentVal);

        return ms.ToArray();
    }

    public static string?[] DecodeString(ReadOnlySpan<byte> compressed)
    {
        if (compressed.IsEmpty)
            return Array.Empty<string?>();

        using var ms = new MemoryStream(compressed.ToArray());
        using var reader = new BinaryReader(ms, Encoding.UTF8);

        int runCount = reader.ReadInt32();
        int totalValues = reader.ReadInt32();

        var result = new string?[totalValues];
        int writeIdx = 0;

        for (int r = 0; r < runCount; r++)
        {
            int count = reader.ReadInt32();
            string? val = ReadString(reader);
            for (int k = 0; k < count; k++)
            {
                result[writeIdx++] = val;
            }
        }

        return result;
    }

    private static void WriteString(BinaryWriter writer, string? val)
    {
        if (val is null)
        {
            writer.Write(-1);
        }
        else
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(val);
            writer.Write(utf8.Length);
            writer.Write(utf8);
        }
    }

    private static string? ReadString(BinaryReader reader)
    {
        int len = reader.ReadInt32();
        if (len < 0) return null;
        byte[] bytes = reader.ReadBytes(len);
        return Encoding.UTF8.GetString(bytes);
    }
}
