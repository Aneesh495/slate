using System.Diagnostics;
using Slate.Core.Execution;
using Slate.Core.Indexing;
using Slate.Core.Primitives;
using Slate.Core.Storage;
using Slate.Engine.Kernel;

namespace Slate.Benchmarks;

public static class Program
{
    public static void Main(string[] args)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("================================================================================");
        Console.WriteLine("                     Slate HTAP Engine Microbenchmarks                         ");
        Console.WriteLine("================================================================================");
        Console.ResetColor();

        BenchmarkVectorSimd();
        BenchmarkCodecs();
        BenchmarkHnswIndex();
        BenchmarkEngineScanAndAggregation();
        BenchmarkMvccIngestion();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n[PASS] All microbenchmark suites finished successfully.");
        Console.ResetColor();
    }

    private static void BenchmarkVectorSimd()
    {
        Console.WriteLine("\n--- 1. Vector SIMD Distance Kernels ---");
        const int dimension = 128;
        const int iterations = 500_000;

        var v1 = new float[dimension];
        var v2 = new float[dimension];
        var rng = new Random(42);
        for (int i = 0; i < dimension; i++)
        {
            v1[i] = (float)rng.NextDouble();
            v2[i] = (float)rng.NextDouble();
        }

        // Warmup
        for (int i = 0; i < 10_000; i++)
        {
            _ = VectorDistance.CosineDistance(v1, v2);
            _ = VectorDistance.EuclideanDistanceSquared(v1, v2);
            _ = VectorDistance.DotProduct(v1, v2);
        }

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < iterations; i++)
        {
            _ = VectorDistance.CosineDistance(v1, v2);
        }
        sw.Stop();
        double cosineOpsSec = iterations / sw.Elapsed.TotalSeconds;

        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            _ = VectorDistance.EuclideanDistanceSquared(v1, v2);
        }
        sw.Stop();
        double euclideanOpsSec = iterations / sw.Elapsed.TotalSeconds;

        sw.Restart();
        for (int i = 0; i < iterations; i++)
        {
            _ = VectorDistance.DotProduct(v1, v2);
        }
        sw.Stop();
        double dotProductOpsSec = iterations / sw.Elapsed.TotalSeconds;

        Console.WriteLine($"  Cosine Distance (128-d):     {cosineOpsSec:N0} ops/sec ({sw.Elapsed.TotalMilliseconds / iterations * 1000.0:F2} ns/op)");
        Console.WriteLine($"  Euclidean Distance (128-d):  {euclideanOpsSec:N0} ops/sec");
        Console.WriteLine($"  Dot Product (128-d):         {dotProductOpsSec:N0} ops/sec");
    }

    private static void BenchmarkCodecs()
    {
        Console.WriteLine("\n--- 2. Columnar Compression Codecs ---");
        const int rowCount = 500_000;
        var intValues = new long[rowCount];
        for (int i = 0; i < rowCount; i++)
        {
            intValues[i] = 1000 + (i % 64); // Suitable for FoR bit-packing
        }

        var rleValues = new long[rowCount];
        for (int i = 0; i < rowCount; i++)
        {
            rleValues[i] = i / 1000; // Repeated runs of 1000
        }

        var floatValues = new double[rowCount];
        for (int i = 0; i < rowCount; i++)
        {
            floatValues[i] = 100.0 + Math.Sin(i * 0.05);
        }

        // BitPacking
        var sw = Stopwatch.StartNew();
        byte[] packedBits = BitPackedCodec.EncodeInt64(intValues);
        sw.Stop();
        double packThroughput = (rowCount * sizeof(long) / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;

        sw.Restart();
        long[] unpacked = BitPackedCodec.DecodeInt64(packedBits);
        sw.Stop();
        double unpackThroughput = (rowCount * sizeof(long) / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;
        Console.WriteLine($"  FoR BitPacking Encode:       {packThroughput:F2} MB/s (Ratio: {(float)(rowCount * 8) / packedBits.Length:F2}x)");
        Console.WriteLine($"  FoR BitPacking Decode:       {unpackThroughput:F2} MB/s");

        // RLE
        sw.Restart();
        byte[] rleEncoded = RleCodec.EncodeInt64(rleValues);
        sw.Stop();
        double rleEncodeThroughput = (rowCount * sizeof(long) / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;

        sw.Restart();
        long[] rleDecoded = RleCodec.DecodeInt64(rleEncoded);
        sw.Stop();
        double rleDecodeThroughput = (rowCount * sizeof(long) / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;
        Console.WriteLine($"  RLE Encode:                  {rleEncodeThroughput:F2} MB/s (Ratio: {(float)(rowCount * 8) / rleEncoded.Length:F2}x)");
        Console.WriteLine($"  RLE Decode:                  {rleDecodeThroughput:F2} MB/s");

        // Gorilla
        sw.Restart();
        byte[] gorillaEncoded = GorillaCodec.EncodeFloat64(floatValues);
        sw.Stop();
        double gorillaEncodeThroughput = (rowCount * sizeof(double) / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;

        sw.Restart();
        double[] gorillaDecoded = GorillaCodec.DecodeFloat64(gorillaEncoded);
        sw.Stop();
        double gorillaDecodeThroughput = (rowCount * sizeof(double) / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;
        Console.WriteLine($"  Gorilla Float64 Encode:      {gorillaEncodeThroughput:F2} MB/s (Ratio: {(float)(rowCount * 8) / gorillaEncoded.Length:F2}x)");
        Console.WriteLine($"  Gorilla Float64 Decode:      {gorillaDecodeThroughput:F2} MB/s");
    }

    private static void BenchmarkHnswIndex()
    {
        Console.WriteLine("\n--- 3. HNSW Hierarchical Navigable Small World Index ---");
        const int numVectors = 5_000;
        const int dimension = 64;
        const int k = 10;
        const int queryCount = 500;

        var rng = new Random(1337);
        var hnsw = new HnswIndex(dimension, VectorMetric.Cosine, m: 16, efConstruction: 64);

        var vectors = new float[numVectors][];
        for (int i = 0; i < numVectors; i++)
        {
            vectors[i] = new float[dimension];
            for (int d = 0; d < dimension; d++)
            {
                vectors[i][d] = (float)rng.NextDouble();
            }
        }

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < numVectors; i++)
        {
            hnsw.Insert(i, vectors[i]);
        }
        sw.Stop();
        Console.WriteLine($"  Indexed {numVectors:N0} {dimension}-d vectors: {sw.ElapsedMilliseconds} ms ({numVectors / sw.Elapsed.TotalSeconds:F0} vec/sec)");

        var queryVectors = new float[queryCount][];
        for (int i = 0; i < queryCount; i++)
        {
            queryVectors[i] = new float[dimension];
            for (int d = 0; d < dimension; d++)
            {
                queryVectors[i][d] = (float)rng.NextDouble();
            }
        }

        sw.Restart();
        for (int i = 0; i < queryCount; i++)
        {
            _ = hnsw.KnnSearch(queryVectors[i], k);
        }
        sw.Stop();
        double qps = queryCount / sw.Elapsed.TotalSeconds;
        double avgLatencyUs = (sw.Elapsed.TotalMilliseconds / queryCount) * 1000.0;
        Console.WriteLine($"  Top-{k} KNN Query:            {qps:F0} QPS (Avg latency: {avgLatencyUs:F1} microseconds)");
    }

    private static void BenchmarkEngineScanAndAggregation()
    {
        Console.WriteLine("\n--- 4. Columnar Scan & Aggregation Engine ---");
        using var db = new SlateDatabase();
        db.Execute("CREATE TABLE bench_metrics (id INT PRIMARY KEY, region STRING, metric FLOAT, revenue FLOAT);");

        const int batchRows = 2_000;
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("INSERT INTO bench_metrics VALUES ");
        for (int i = 0; i < batchRows; i++)
        {
            string region = (i % 2 == 0) ? "us-east" : "eu-west";
            sb.Append($"({i}, '{region}', {i * 1.5}, {i * 10.0})");
            sb.AppendLine(i < batchRows - 1 ? "," : ";");
        }

        var sw = Stopwatch.StartNew();
        db.Execute(sb.ToString());
        sw.Stop();
        Console.WriteLine($"  Ingested {batchRows:N0} rows:        {sw.ElapsedMilliseconds} ms ({batchRows / sw.Elapsed.TotalSeconds:N0} rows/sec)");

        // Scan + Filter + Aggregation
        sw.Restart();
        for (int i = 0; i < 50; i++)
        {
            _ = db.Execute("SELECT region, SUM(revenue), AVG(metric) FROM bench_metrics GROUP BY region;");
        }
        sw.Stop();
        double throughput = (50.0 * batchRows) / sw.Elapsed.TotalSeconds;
        Console.WriteLine($"  Grouped Aggregation Scan:    {throughput:N0} rows/sec ({sw.Elapsed.TotalMilliseconds / 50.0:F2} ms/query)");
    }

    private static void BenchmarkMvccIngestion()
    {
        Console.WriteLine("\n--- 5. MVCC Concurrent Transaction Throughput ---");
        using var db = new SlateDatabase();
        db.Execute("CREATE TABLE accounts (id INT PRIMARY KEY, balance FLOAT);");

        const int txCount = 2_000;
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < txCount; i++)
        {
            var tx = db.BeginTransaction();
            db.Execute($"INSERT INTO accounts VALUES ({i}, {1000.0 + i});", tx);
            db.CommitTransaction(tx);
        }
        sw.Stop();
        double txPerSec = txCount / sw.Elapsed.TotalSeconds;
        Console.WriteLine($"  Committed Transactions:      {txPerSec:N0} tx/sec ({sw.Elapsed.TotalMilliseconds / txCount * 1000.0:F1} microseconds/tx)");
    }
}
