using Slate.Core.Storage;
using Xunit;

namespace Slate.Tests;

public sealed class RleCodecTests
{
    [Fact]
    public void RleCodec_Int64Runs_CompressesAndDecompressesAccurately()
    {
        var original = new long[] { 10, 10, 10, 10, 25, 25, 42, 42, 42, 42, 42 };
        byte[] compressed = RleCodec.EncodeInt64(original);

        // 3 runs * 12 bytes + 8 bytes header = 44 bytes
        // Original was 11 * 8 = 88 bytes
        Assert.True(compressed.Length < original.Length * sizeof(long));

        long[] decoded = RleCodec.DecodeInt64(compressed);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void RleCodec_StringRuns_HandlesNullsAndRepeats()
    {
        var original = new string?[] { "US", "US", "US", "UK", "UK", null, null, "CA" };
        byte[] compressed = RleCodec.EncodeString(original);
        string?[] decoded = RleCodec.DecodeString(compressed);

        Assert.Equal(original, decoded);
    }

    [Fact]
    public void RleCodec_EmptyInput_ReturnsEmptyArray()
    {
        var emptyInts = Array.Empty<long>();
        byte[] compressed = RleCodec.EncodeInt64(emptyInts);
        Assert.Empty(compressed);

        long[] decoded = RleCodec.DecodeInt64(compressed);
        Assert.Empty(decoded);
    }
}
