using System.Buffers.Binary;
using System.Numerics;

namespace Slate.Core.Storage;

/// <summary>
/// Lossless Gorilla floating-point XOR compression codec.
/// Compresses IEEE-754 Float64 sequences by XORing consecutive values and encoding
/// leading/trailing zero runs into a compact bitstream.
/// </summary>
public static class GorillaCodec
{
    private sealed class BitWriter
    {
        private readonly List<byte> _bytes = new();
        private byte _currentByte;
        private int _bitsInByte;

        public void WriteBit(int bit)
        {
            _currentByte |= (byte)((bit & 1) << (7 - _bitsInByte));
            _bitsInByte++;
            if (_bitsInByte == 8)
            {
                _bytes.Add(_currentByte);
                _currentByte = 0;
                _bitsInByte = 0;
            }
        }

        public void WriteBits(ulong value, int bitCount)
        {
            for (int i = bitCount - 1; i >= 0; i--)
            {
                WriteBit((int)((value >> i) & 1));
            }
        }

        public byte[] ToArray()
        {
            if (_bitsInByte > 0)
            {
                _bytes.Add(_currentByte);
            }
            return _bytes.ToArray();
        }
    }

    private sealed class BitReader
    {
        private readonly ReadOnlyMemory<byte> _bytes;
        private int _byteOffset;
        private int _bitOffset;

        public BitReader(ReadOnlyMemory<byte> bytes)
        {
            _bytes = bytes;
            _byteOffset = 0;
            _bitOffset = 0;
        }

        public int ReadBit()
        {
            if (_byteOffset >= _bytes.Length) return 0;
            int bit = (_bytes.Span[_byteOffset] >> (7 - _bitOffset)) & 1;
            _bitOffset++;
            if (_bitOffset == 8)
            {
                _byteOffset++;
                _bitOffset = 0;
            }
            return bit;
        }

        public ulong ReadBits(int bitCount)
        {
            ulong result = 0;
            for (int i = 0; i < bitCount; i++)
            {
                result = (result << 1) | (ulong)(uint)ReadBit();
            }
            return result;
        }
    }

    public static byte[] EncodeFloat64(ReadOnlySpan<double> values)
    {
        if (values.IsEmpty)
            return Array.Empty<byte>();

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        // Header: [count: 4 bytes]
        bw.Write(values.Length);

        var writer = new BitWriter();

        // Write first value uncompressed (64 bits)
        ulong previousBits = (ulong)BitConverter.DoubleToInt64Bits(values[0]);
        writer.WriteBits(previousBits, 64);

        int previousLeadingZeros = int.MaxValue;
        int previousTrailingZeros = int.MaxValue;

        for (int i = 1; i < values.Length; i++)
        {
            ulong currentBits = (ulong)BitConverter.DoubleToInt64Bits(values[i]);
            ulong xor = currentBits ^ previousBits;

            if (xor == 0)
            {
                // '0' bit: identical to previous
                writer.WriteBit(0);
            }
            else
            {
                // '1' bit: value differs
                writer.WriteBit(1);

                int leadingZeros = BitOperations.LeadingZeroCount(xor);
                int trailingZeros = BitOperations.TrailingZeroCount(xor);

                // Bound leading zeros to 31 for 5-bit encoding
                if (leadingZeros > 31) leadingZeros = 31;

                if (previousLeadingZeros != int.MaxValue &&
                    leadingZeros >= previousLeadingZeros &&
                    trailingZeros >= previousTrailingZeros)
                {
                    // Control bit '0': reuse previous window
                    writer.WriteBit(0);
                    int meaningfulBits = 64 - previousLeadingZeros - previousTrailingZeros;
                    ulong valueToWrite = xor >> previousTrailingZeros;
                    writer.WriteBits(valueToWrite, meaningfulBits);
                }
                else
                {
                    // Control bit '1': new window
                    writer.WriteBit(1);
                    writer.WriteBits((ulong)leadingZeros, 5); // 5 bits for leading zeros

                    int meaningfulBits = 64 - leadingZeros - trailingZeros;
                    writer.WriteBits((ulong)meaningfulBits, 6); // 6 bits for meaningful length (1..64)

                    ulong valueToWrite = xor >> trailingZeros;
                    writer.WriteBits(valueToWrite, meaningfulBits);

                    previousLeadingZeros = leadingZeros;
                    previousTrailingZeros = trailingZeros;
                }
            }

            previousBits = currentBits;
        }

        byte[] bitStream = writer.ToArray();
        bw.Write(bitStream.Length);
        bw.Write(bitStream);

        return ms.ToArray();
    }

    public static double[] DecodeFloat64(ReadOnlySpan<byte> compressed)
    {
        if (compressed.IsEmpty)
            return Array.Empty<double>();

        int count = BinaryPrimitives.ReadInt32LittleEndian(compressed[0..4]);
        int bitStreamLen = BinaryPrimitives.ReadInt32LittleEndian(compressed[4..8]);

        var bitSpan = compressed.Slice(8, bitStreamLen);
        var reader = new BitReader(bitSpan.ToArray());

        var result = new double[count];
        if (count == 0) return result;

        ulong previousBits = reader.ReadBits(64);
        result[0] = BitConverter.Int64BitsToDouble((long)previousBits);

        int previousLeadingZeros = 0;
        int previousTrailingZeros = 0;

        for (int i = 1; i < count; i++)
        {
            int bit = reader.ReadBit();
            if (bit == 0)
            {
                // Identical to previous
                result[i] = BitConverter.Int64BitsToDouble((long)previousBits);
            }
            else
            {
                int controlBit = reader.ReadBit();
                ulong xor;

                if (controlBit == 0)
                {
                    // Reuse previous window
                    int meaningfulBits = 64 - previousLeadingZeros - previousTrailingZeros;
                    ulong bits = reader.ReadBits(meaningfulBits);
                    xor = bits << previousTrailingZeros;
                }
                else
                {
                    // New window
                    int leadingZeros = (int)reader.ReadBits(5);
                    int meaningfulBits = (int)reader.ReadBits(6);
                    if (meaningfulBits == 0) meaningfulBits = 64; // Handle full 64-bit case

                    int trailingZeros = 64 - leadingZeros - meaningfulBits;
                    ulong bits = reader.ReadBits(meaningfulBits);
                    xor = bits << trailingZeros;

                    previousLeadingZeros = leadingZeros;
                    previousTrailingZeros = trailingZeros;
                }

                ulong currentBits = previousBits ^ xor;
                result[i] = BitConverter.Int64BitsToDouble((long)currentBits);
                previousBits = currentBits;
            }
        }

        return result;
    }
}
