using Slate.Core.Execution;
using Slate.Core.Indexing;
using Slate.Core.Primitives;
using Slate.Core.Query;
using Xunit;

namespace Slate.Tests;

public sealed class ScanTests
{
    private TableSchema CreateSchema() => new(
        "users",
        new[]
        {
            new ColumnDescriptor("id", DataType.Int64, isNullable: false, ordinal: 0),
            new ColumnDescriptor("name", DataType.String, isNullable: false, ordinal: 1),
            new ColumnDescriptor("score", DataType.Int64, isNullable: false, ordinal: 2)
        },
        primaryKeyName: "id");

    private VectorBatch CreateBatch(TableSchema schema, int startId, int count)
    {
        var colId = new ColumnVector(DataType.Int64, count);
        var colName = new ColumnVector(DataType.String, count);
        var colScore = new ColumnVector(DataType.Int64, count);

        for (int i = 0; i < count; i++)
        {
            long id = startId + i;
            colId.AppendInt64(id);
            colName.AppendString($"User_{id}");
            colScore.AppendInt64(id * 10);
        }

        return new VectorBatch(schema, new[] { colId, colName, colScore }, count);
    }

    [Fact]
    public void TableScanOperator_ProjectionsAndFiltering_EmitsProjectedBatches()
    {
        var schema = CreateSchema();
        var batch1 = CreateBatch(schema, 1, 50);
        var batch2 = CreateBatch(schema, 51, 50);

        // Project only id and score, with pushed predicate score >= 800
        var pred = new BinaryExpression(
            new ColumnReferenceExpression(null, "score"),
            BinaryOp.GreaterThanOrEqual,
            new LiteralExpression(SlateValue.FromInt64(800)));

        using var scan = new TableScanOperator(
            schema,
            new[] { batch1, batch2 },
            projectedColumnNames: new List<string> { "id", "score" },
            pushedPredicate: pred);

        var ctx = new QueryExecutionContext();
        scan.Open(ctx);

        var resultBatch = scan.Next();
        Assert.NotNull(resultBatch);
        Assert.Equal(2, resultBatch.Columns.Length);
        Assert.Equal("id", resultBatch.Schema.Columns[0].Name);
        Assert.Equal("score", resultBatch.Schema.Columns[1].Name);

        // Batch 1 has scores 10..500 -> 0 rows match
        // Batch 2 has scores 510..1000 -> ids 80..100 match (21 rows)
        Assert.NotNull(resultBatch.SelectionVector);
        Assert.Equal(21, resultBatch.SelectionVector.Length);

        Assert.Null(scan.Next()); // EOF
        scan.Close();
    }

    [Fact]
    public void IndexScanOperator_PointLookupAndRangeScan_ExecutesFastRetrieval()
    {
        var schema = CreateSchema();
        var btree = new BPlusTree<SlateValue, SlateValue[]>(maxDegree: 8);

        for (int i = 1; i <= 100; i++)
        {
            var key = SlateValue.FromInt64(i);
            var vals = new[] { key, SlateValue.FromString($"Name_{i}"), SlateValue.FromInt64(i * 100) };
            btree.Insert(key, vals);
        }

        // Point lookup for id = 42
        using var pointScan = new IndexScanOperator(schema, btree, pointLookupKey: SlateValue.FromInt64(42));
        var ctx = new QueryExecutionContext();
        pointScan.Open(ctx);

        var batch = pointScan.Next();
        Assert.NotNull(batch);
        Assert.Equal(1, batch.RowCount);
        Assert.Equal(42L, batch.Columns[0].GetValue(0).AsInt64());
        Assert.Equal("Name_42", batch.Columns[1].GetValue(0).AsString());
        pointScan.Close();

        // Range scan for id in [20, 30]
        using var rangeScan = new IndexScanOperator(
            schema,
            btree,
            startKey: SlateValue.FromInt64(20),
            endKey: SlateValue.FromInt64(30));

        rangeScan.Open(ctx);
        var rangeBatch = rangeScan.Next();
        Assert.NotNull(rangeBatch);
        Assert.Equal(11, rangeBatch.RowCount); // 20 through 30 inclusive = 11 rows
        Assert.Equal(20L, rangeBatch.Columns[0].GetValue(0).AsInt64());
        Assert.Equal(30L, rangeBatch.Columns[0].GetValue(10).AsInt64());
        rangeScan.Close();
    }
}
