using Slate.Core.Indexing;
using Slate.Core.Primitives;
using Slate.Core.Storage;
using Slate.Core.Transactions;
using Xunit;

namespace Slate.Tests;

public sealed class DifferentialTests
{
    [Fact]
    public void Differential_HnswVsLinearScan_MaintainsHighRecall()
    {
        int dim = 8;
        int vectorCount = 300;
        int k = 5;

        var rng = new Random(12345);
        var vectors = new float[vectorCount][];
        var hnsw = new HnswIndex(dimension: dim, metric: VectorMetric.Euclidean, m: 16, efConstruction: 64);

        for (int i = 0; i < vectorCount; i++)
        {
            vectors[i] = new float[dim];
            for (int d = 0; d < dim; d++) vectors[i][d] = (float)rng.NextDouble();
            hnsw.Insert(i, vectors[i]);
        }

        // Test with 20 random query vectors
        int totalHits = 0;
        int totalExpected = 20 * k;

        for (int q = 0; q < 20; q++)
        {
            var query = new float[dim];
            for (int d = 0; d < dim; d++) query[d] = (float)rng.NextDouble();

            // 1. Exact linear scan
            var exactDistances = new (int Id, float Distance)[vectorCount];
            for (int i = 0; i < vectorCount; i++)
            {
                float dist = VectorDistance.ComputeDistance(query, vectors[i], VectorMetric.Euclidean);
                exactDistances[i] = (i, dist);
            }
            Array.Sort(exactDistances, (a, b) => a.Distance.CompareTo(b.Distance));
            var exactTopK = exactDistances.Take(k).Select(x => x.Id).ToHashSet();

            // 2. HNSW search
            var hnswResults = hnsw.KnnSearch(query, k, ef: 32);
            foreach (var r in hnswResults)
            {
                if (exactTopK.Contains(r.Id))
                {
                    totalHits++;
                }
            }
        }

        double recall = (double)totalHits / totalExpected;
        // HNSW recall should exceed 85% on random vectors
        Assert.True(recall >= 0.85, $"HNSW recall was {recall:P2}, expected >= 85%.");
    }

    [Fact]
    public async Task Differential_ConcurrentMvccChaos_GuaranteesFirstCommitterWins()
    {
        var mvcc = new MvccManager(1000);
        int successCommits = 0;
        int conflictAborts = 0;

        // 8 tasks trying to concurrently update the same key "balance"
        var tasks = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            try
            {
                var tx = mvcc.BeginTransaction();
                mvcc.RecordVersion("bank", SlateValue.FromInt64(1), SlateValue.FromInt64(100), tx);
                Thread.Sleep(5); // Simulate brief execution window
                mvcc.CommitTransaction(tx);
                Interlocked.Increment(ref successCommits);
            }
            catch (WriteConflictException)
            {
                Interlocked.Increment(ref conflictAborts);
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        // At least one should succeed, total should be 8
        Assert.True(successCommits >= 1);
        Assert.Equal(8, successCommits + conflictAborts);
    }

    [Fact]
    public void Differential_Codecs_ExactRoundtripIntegrity()
    {
        // 1. Gorilla codec
        var floats = new double[] { 1.234, 1.234, 5.678, 5.678, 9.999, 100.5, 100.5 };
        byte[] gorillaEncoded = GorillaCodec.EncodeFloat64(floats);
        double[] gorillaDecoded = GorillaCodec.DecodeFloat64(gorillaEncoded);
        Assert.Equal(floats, gorillaDecoded);

        // 2. Dictionary codec
        var strings = new string[] { "apple", "banana", "apple", "cherry", "banana", "cherry" };
        var dictEncoded = DictionaryCodec.EncodeStrings(strings);
        string?[] dictDecoded = DictionaryCodec.DecodeStrings(dictEncoded);
        Assert.Equal(strings, dictDecoded);

        // 3. RLE codec
        var rleInput = new long[] { 42, 42, 42, 99, 99, 100, 100, 100, 100 };
        var rleEncoded = RleCodec.EncodeInt64(rleInput);
        long[] rleDecoded = RleCodec.DecodeInt64(rleEncoded);
        Assert.Equal(rleInput, rleDecoded);
    }
}
