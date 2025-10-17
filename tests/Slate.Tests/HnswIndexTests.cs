using Slate.Core.Indexing;
using Xunit;

namespace Slate.Tests;

public sealed class HnswIndexTests
{
    [Fact]
    public void HnswIndex_KnnSearch_FindsExactNearestNeighbors()
    {
        // 8-dimensional space
        int dim = 8;
        var hnsw = new HnswIndex(dim, VectorMetric.Euclidean, m: 16, efConstruction: 64);

        // Generate synthetic clusters
        var rng = new Random(42);
        for (int i = 0; i < 200; i++)
        {
            var vec = new float[dim];
            for (int d = 0; d < dim; d++) vec[d] = (float)rng.NextDouble();
            hnsw.Insert(i, vec);
        }

        Assert.Equal(200, hnsw.Count);

        // Target vector matching node 42 exactly
        var query = new float[dim];
        for (int d = 0; d < dim; d++) query[d] = 0.5f;
        hnsw.Insert(999, query);

        var results = hnsw.KnnSearch(query, k: 5);
        Assert.NotEmpty(results);
        Assert.Equal(999, results[0].Id);
        Assert.True(results[0].Distance < 1e-5f); // Distance to itself is 0
    }

    [Fact]
    public void HnswIndex_CosineMetric_HandlesNormalizedVectors()
    {
        int dim = 4;
        var hnsw = new HnswIndex(dim, VectorMetric.Cosine);

        hnsw.Insert(1, new float[] { 1f, 0f, 0f, 0f });
        hnsw.Insert(2, new float[] { 0f, 1f, 0f, 0f });
        hnsw.Insert(3, new float[] { 0.7071f, 0.7071f, 0f, 0f }); // 45 degrees between 1 and 2

        var results = hnsw.KnnSearch(new float[] { 1f, 0.1f, 0f, 0f }, k: 2);
        Assert.Equal(2, results.Count);
        // Node 1 should be closest
        Assert.Equal(1, results[0].Id);
    }
}
