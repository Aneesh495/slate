using Slate.Core.Indexing;
using Xunit;

namespace Slate.Tests;

public sealed class VectorDistanceTests
{
    [Fact]
    public void VectorDistance_CosineIdenticalVectors_YieldsZeroDistance()
    {
        var v1 = new float[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f };
        var v2 = new float[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f };

        float dist = VectorDistance.ComputeDistance(v1, v2, VectorMetric.Cosine);
        Assert.True(MathF.Abs(dist) < 1e-5f);
    }

    [Fact]
    public void VectorDistance_CosineOrthogonalVectors_YieldsDistanceOne()
    {
        var v1 = new float[] { 1f, 0f, 0f, 0f };
        var v2 = new float[] { 0f, 1f, 0f, 0f };

        float dist = VectorDistance.ComputeDistance(v1, v2, VectorMetric.Cosine);
        Assert.Equal(1f, dist, precision: 5);
    }

    [Fact]
    public void VectorDistance_EuclideanDistance_MatchesPythagoras()
    {
        var v1 = new float[] { 0f, 0f };
        var v2 = new float[] { 3f, 4f };

        // 3^2 + 4^2 = 25
        float distSq = VectorDistance.ComputeDistance(v1, v2, VectorMetric.Euclidean);
        Assert.Equal(25f, distSq, precision: 5);
    }
}
