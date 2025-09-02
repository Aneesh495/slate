using System.Buffers.Binary;
using System.Numerics;

namespace Slate.Core.Storage;

/// <summary>
/// Frame-of-Reference (FoR) bit-packing codec for integer columns.
/// Subtracts the block minimum and packs residual deltas into the minimal number of bits required.
/// </summary>
public static class BitPackedCodec
{
    public static byte[] EncodeInt64(ReadOnlySpan<long> values)
    {
        if (values.IsEmpty)
            return Array.Empty<byte>();

        long minValue = values[0];
        long maxValue = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] < minValue) minValue = values[i];
            if (values[i] > maxValue) maxValue = values[i];
        }

        ulong maxDelta = (ulong)(maxValue - minValue);
        int bitWidth = maxDelta == 0 ? 0 : 64 - BitOperations.LeadingZeroCount(maxDelta);

        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        // Header: [bitWidth: 1 byte, reserved: 3 bytes, count: 4 bytes, minValue: 8 bytes]
        writer.Write((byte)bitWidth);
        writer.Write((byte)0);
        writer.Write((ushort)0);
        writer.Write(values.Length);
        writer.Write(minValue);

        if (bitWidth == 0)
        {
            // All values identical to minValue; no bitstream required
            return ms.ToArray();
        }

        // Pack bits into 64-bit words (LSB first)
        ulong currentWord = 0;
        int bitsInWord = 0;

        for (int i = 0; i < values.Length; i++)
        {
            ulong delta = (ulong)(values[i] - minValue);

            if (bitsInWord + bitWidth <= 64)
            {
                currentWord |= (delta << bitsInWord);
                bitsInWord += bitWidth;
                if (bitsInWord == 64)
                {
                    writer.Write(currentWord);
                    currentWord = 0;
                    bitsInWord = 0;
                }
            }
            else
            {
                int bitsRemaining = 64 - bitsInWord;
                currentWord |= ((delta & ((1UL << bitsRemaining) - 1)) << bitsInWord);
                writer.Write(currentWord);

                int overflowBits = bitWidth - bitsRemaining;
                currentWord = delta >> bitsRemaining;
                bitsInWord = overflowBits;
            }
        }

        if (bitsInWord > 0)
        {
            writer.Write(currentWord);
        }

        return ms.ToArray();
    }

    public static long[] DecodeInt64(ReadOnlySpan<byte> compressed)
    {
        if (compressed.IsEmpty)
            return Array.Empty<long>();

        byte bitWidth = compressed[0];
        int count = BinaryPrimitives.ReadInt32LittleEndian(compressed.Slice(4, 4));
        long minValue = BinaryPrimitives.ReadInt64LittleEndian(compressed.Slice(8, 8));

        var result = new long[count];
        if (bitWidth == 0)
        {
            Array.Fill(result, minValue);
            return result;
        }

        ulong mask = bitWidth == 64 ? ulong.MaxValue : (1UL << bitWidth) - 1;
        int wordOffset = 16;
        ulong currentWord = BinaryPrimitives.ReadUInt64LittleEndian(compressed.Slice(wordOffset, 8));
        wordOffset += 8;
        int bitsAvailable = 64;

        for (int i = 0; i < count; i++)
        {
            if (bitsAvailable == 0)
            {
                currentWord = wordOffset < compressed.Length
                    ? BinaryPrimitives.ReadUInt64LittleEndian(compressed.Slice(wordOffset, 8))
                    : 0UL;
                wordOffset += 8;
                bitsAvailable = 64;
            }

            if (bitsAvailable >= bitWidth)
            {
                ulong delta = (currentWord >> (64 - bitsAvailable)) & mask;
                result[i] = minValue + (long)delta;
                bitsAvailable -= bitWidth;
            }
            else
            {
                // Value spans across current word and next word
                int firstPartBits = bitsAvailable;
                ulong firstPart = (currentWord >> (64 - firstPartBits));

                currentWord = wordOffset < compressed.Length
                    ? BinaryPrimitives.ReadUInt64LittleEndian(compressed.Slice(wordOffset, 8))
                    : 0UL;
                wordOffset += 8;

                int secondPartBits = bitWidth - firstPartBits;
                ulong secondPart = currentWord & ((1UL << secondPartBits) - 1);
                ulong delta = (secondPart << firstPartBits) | firstPart;
                result[i] = minValue + (long)delta;
                bitsAvailable = 64 - secondPartBits;
            }
        }

        return result;
    }
}
