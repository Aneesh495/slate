using Slate.Core.Execution;
using Slate.Core.Indexing;
using Slate.Core.Primitives;
using Slate.Core.Query;
using Xunit;

namespace Slate.Tests;

public sealed class VectorKnnTests
{
    private (TableSchema, VectorBatch) CreateVectorDataset()
    {
        var schema = new TableSchema("embeddings", new[]
        {
            new ColumnDescriptor("id", DataType.Int64, isNullable: false, ordinal: 0),
            new ColumnDescriptor("vec", DataType.VectorFloat32, isNullable: false, ordinal: 1, vectorDimension: 4)
        }, primaryKeyName: "id");

        var colId = new ColumnVector(DataType.Int64, 4);
        var colVec = new ColumnVector(DataType.VectorFloat32, 4);

        colId.AppendInt64(1); colVec.AppendVector(new float[] { 1.0f, 0.0f, 0.0f, 0.0f });
        colId.AppendInt64(2); colVec.AppendVector(new float[] { 0.0f, 1.0f, 0.0f, 0.0f });
        colId.AppendInt64(3); colVec.AppendVector(new float[] { 0.9f, 0.1f, 0.0f, 0.0f }); // Very close to ID 1
        colId.AppendInt64(4); colVec.AppendVector(new float[] { 0.0f, 0.0f, 1.0f, 0.0f });

        return (schema, new VectorBatch(schema, new[] { colId, colVec }, 4));
    }

    [Fact]
    public void VectorKnnOperator_BruteForceScan_ReturnsTopKNearestNeighbors()
    {
        var (schema, batch) = CreateVectorDataset();
        using var scan = new TableScanOperator(schema, new[] { batch });

        // Query vector: [1.0, 0.0, 0.0, 0.0] -> Expect ID 1 (dist 0), then ID 3 (dist ~0.14)
        float[] queryVec = { 1.0f, 0.0f, 0.0f, 0.0f };

        using var knn = new VectorKnnOperator(scan, "vec", VectorDistanceMetric.L2, queryVec, k: 2);
        var ctx = new QueryExecutionContext();
        knn.Open(ctx);

        var result = knn.Next();
        Assert.NotNull(result);
        Assert.Equal(2, result.RowCount);
        Assert.Equal(3, result.Schema.ColumnCount); // id, vec, distance
        Assert.Equal("distance", result.Schema.Columns[2].Name);

        // Nearest: ID 1 with distance 0.0
        Assert.Equal(1L, result.GetValue(0, 0).AsInt64());
        Assert.Equal(0.0f, (float)result.GetValue(2, 0).AsFloat64(), precision: 3);

        // Second nearest: ID 3
        Assert.Equal(3L, result.GetValue(0, 1).AsInt64());
        Assert.True(result.GetValue(2, 1).AsFloat64() > 0.0);

        Assert.Null(knn.Next());
        knn.Close();
    }

    [Fact]
    public void VectorKnnOperator_HnswIndex_PerformsAcceleratedSearch()
    {
        var schema = new TableSchema("docs", new[]
        {
            new ColumnDescriptor("id", DataType.Int64, isNullable: false, ordinal: 0),
            new ColumnDescriptor("vec", DataType.VectorFloat32, isNullable: false, ordinal: 1, vectorDimension: 4)
        }, primaryKeyName: "id");

        var hnsw = new HnswIndex(dimension: 4, metric: VectorMetric.Euclidean);
        hnsw.Insert(1, new float[] { 1.0f, 0.0f, 0.0f, 0.0f });
        hnsw.Insert(2, new float[] { 0.0f, 1.0f, 0.0f, 0.0f });
        hnsw.Insert(3, new float[] { 0.8f, 0.2f, 0.0f, 0.0f });

        float[] queryVec = { 1.0f, 0.0f, 0.0f, 0.0f };
        using var knn = new VectorKnnOperator(hnsw, schema, "vec", VectorDistanceMetric.L2, queryVec, k: 2);

        var ctx = new QueryExecutionContext();
        knn.Open(ctx);

        var result = knn.Next();
        Assert.NotNull(result);
        Assert.Equal(2, result.RowCount);
        Assert.Equal(1L, result.GetValue(0, 0).AsInt64());

        Assert.Null(knn.Next());
        knn.Close();
    }
}
