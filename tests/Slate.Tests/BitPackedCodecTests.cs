using Slate.Core.Storage;
using Xunit;

namespace Slate.Tests;

public sealed class BitPackedCodecTests
{
    [Fact]
    public void BitPackedCodec_SmallDeltas_CompressesDramatically()
    {
        // 1000 values in range [1000000, 1000015] require only 4 bits per element!
        var original = new long[1000];
        for (int i = 0; i < original.Length; i++)
        {
            original[i] = 1_000_000L + (i % 16);
        }

        byte[] compressed = BitPackedCodec.EncodeInt64(original);

        // 1000 * 4 bits = 4000 bits = 500 bytes + 16 header = 516 bytes (vs 8000 bytes uncompressed)
        Assert.True(compressed.Length < 600);

        long[] decoded = BitPackedCodec.DecodeInt64(compressed);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void BitPackedCodec_ConstantValues_BitWidthZero()
    {
        var original = new long[500];
        Array.Fill(original, 999999L);

        byte[] compressed = BitPackedCodec.EncodeInt64(original);
        // Header only (16 bytes)
        Assert.Equal(16, compressed.Length);

        long[] decoded = BitPackedCodec.DecodeInt64(compressed);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void BitPackedCodec_RandomArbitraryInt64_LosslessRoundtrip()
    {
        var rng = new Random(1337);
        var original = new long[256];
        for (int i = 0; i < original.Length; i++)
        {
            original[i] = rng.NextInt64(-500_000L, 500_000L);
        }

        byte[] compressed = BitPackedCodec.EncodeInt64(original);
        long[] decoded = BitPackedCodec.DecodeInt64(compressed);
        Assert.Equal(original, decoded);
    }
}
