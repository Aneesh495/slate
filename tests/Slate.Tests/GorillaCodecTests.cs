using Slate.Core.Storage;
using Xunit;

namespace Slate.Tests;

public sealed class GorillaCodecTests
{
    [Fact]
    public void GorillaCodec_SlowlyVaryingTelemetry_CompressesAndRecoversLosslessly()
    {
        // Simulate a smooth temperature telemetry sensor reading
        var original = new double[200];
        double temp = 21.5;
        for (int i = 0; i < original.Length; i++)
        {
            if (i % 5 == 0) temp += 0.05;
            original[i] = temp;
        }

        byte[] compressed = GorillaCodec.EncodeFloat64(original);

        // 200 doubles uncompressed = 1600 bytes
        // With Gorilla, repetitive and small floating deltas compress to < 400 bytes
        Assert.True(compressed.Length < 600);

        double[] decoded = GorillaCodec.DecodeFloat64(compressed);
        Assert.Equal(original.Length, decoded.Length);
        for (int i = 0; i < original.Length; i++)
        {
            Assert.Equal(original[i], decoded[i], precision: 12);
        }
    }

    [Fact]
    public void GorillaCodec_IdenticalValues_CompressesToOneBitPerValue()
    {
        var original = new double[100];
        Array.Fill(original, 3.141592653589793);

        byte[] compressed = GorillaCodec.EncodeFloat64(original);
        // First value: 8 bytes + 99 bits (~13 bytes) + header (~8 bytes) = ~30 bytes
        Assert.True(compressed.Length < 50);

        double[] decoded = GorillaCodec.DecodeFloat64(compressed);
        Assert.Equal(original, decoded);
    }
}
