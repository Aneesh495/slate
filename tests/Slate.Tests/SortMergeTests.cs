using Slate.Core.Execution;
using Slate.Core.Primitives;
using Slate.Core.Query;
using Xunit;

namespace Slate.Tests;

public sealed class SortMergeTests
{
    private TableSchema CreateSchema() => new(
        "records",
        new[]
        {
            new ColumnDescriptor("id", DataType.Int64, isNullable: false, ordinal: 0),
            new ColumnDescriptor("val", DataType.Int64, isNullable: false, ordinal: 1)
        });

    [Fact]
    public void SortOperator_AscendingAndDescendingWithLimit_SortsAccurately()
    {
        var schema = CreateSchema();
        var colId = new ColumnVector(DataType.Int64, 5);
        var colVal = new ColumnVector(DataType.Int64, 5);

        // Unsorted values: [50, 10, 40, 20, 30]
        long[] ids = { 1, 2, 3, 4, 5 };
        long[] vals = { 50, 10, 40, 20, 30 };

        for (int i = 0; i < 5; i++)
        {
            colId.AppendInt64(ids[i]);
            colVal.AppendInt64(vals[i]);
        }
        var batch = new VectorBatch(schema, new[] { colId, colVal }, 5);

        // 1. Sort ASC on val
        using var scan1 = new TableScanOperator(schema, new[] { batch });
        using var sortAsc = new SortOperator(scan1, new List<OrderByClause>
        {
            new OrderByClause(new ColumnReferenceExpression(null, "val"), Ascending: true)
        });

        var ctx = new QueryExecutionContext();
        sortAsc.Open(ctx);
        var ascBatch = sortAsc.Next();
        Assert.NotNull(ascBatch);
        Assert.Equal(5, ascBatch.RowCount);
        Assert.Equal(10L, ascBatch.GetValue(1, 0).AsInt64());
        Assert.Equal(20L, ascBatch.GetValue(1, 1).AsInt64());
        Assert.Equal(30L, ascBatch.GetValue(1, 2).AsInt64());
        Assert.Equal(40L, ascBatch.GetValue(1, 3).AsInt64());
        Assert.Equal(50L, ascBatch.GetValue(1, 4).AsInt64());
        sortAsc.Close();

        // 2. Sort DESC on val with LIMIT 3
        using var scan2 = new TableScanOperator(schema, new[] { batch });
        using var sortDesc = new SortOperator(scan2, new List<OrderByClause>
        {
            new OrderByClause(new ColumnReferenceExpression(null, "val"), Ascending: false)
        }, limit: 3);

        sortDesc.Open(ctx);
        var descBatch = sortDesc.Next();
        Assert.NotNull(descBatch);
        Assert.Equal(3, descBatch.RowCount);
        Assert.Equal(50L, descBatch.GetValue(1, 0).AsInt64());
        Assert.Equal(40L, descBatch.GetValue(1, 1).AsInt64());
        Assert.Equal(30L, descBatch.GetValue(1, 2).AsInt64());
        sortDesc.Close();
    }

    [Fact]
    public void SortMergeJoinOperator_SortedStreams_MergesLinearly()
    {
        var schemaA = new TableSchema("a", new[]
        {
            new ColumnDescriptor("k", DataType.Int64, isNullable: false, ordinal: 0),
            new ColumnDescriptor("v_a", DataType.String, isNullable: false, ordinal: 1)
        });
        var schemaB = new TableSchema("b", new[]
        {
            new ColumnDescriptor("k", DataType.Int64, isNullable: false, ordinal: 0),
            new ColumnDescriptor("v_b", DataType.String, isNullable: false, ordinal: 1)
        });

        var colKa = new ColumnVector(DataType.Int64, 3);
        var colVa = new ColumnVector(DataType.String, 3);
        colKa.AppendInt64(1); colVa.AppendString("A1");
        colKa.AppendInt64(2); colVa.AppendString("A2");
        colKa.AppendInt64(4); colVa.AppendString("A4");
        var batchA = new VectorBatch(schemaA, new[] { colKa, colVa }, 3);

        var colKb = new ColumnVector(DataType.Int64, 3);
        var colVb = new ColumnVector(DataType.String, 3);
        colKb.AppendInt64(2); colVb.AppendString("B2");
        colKb.AppendInt64(3); colVb.AppendString("B3");
        colKb.AppendInt64(4); colVb.AppendString("B4");
        var batchB = new VectorBatch(schemaB, new[] { colKb, colVb }, 3);

        using var scanA = new TableScanOperator(schemaA, new[] { batchA });
        using var scanB = new TableScanOperator(schemaB, new[] { batchB });

        using var smj = new SortMergeJoinOperator(
            leftChild: scanA,
            rightChild: scanB,
            leftJoinColumn: "k",
            rightJoinColumn: "k",
            joinType: JoinType.Inner);

        var ctx = new QueryExecutionContext();
        smj.Open(ctx);

        var result = smj.Next();
        Assert.NotNull(result);

        // Keys 2 and 4 match -> 2 rows
        Assert.Equal(2, result.RowCount);
        // Match 1: k=2
        Assert.Equal(2L, result.GetValue(0, 0).AsInt64());
        Assert.Equal("A2", result.GetValue(1, 0).AsString());
        Assert.Equal("B2", result.GetValue(3, 0).AsString());

        // Match 2: k=4
        Assert.Equal(4L, result.GetValue(0, 1).AsInt64());
        Assert.Equal("A4", result.GetValue(1, 1).AsString());
        Assert.Equal("B4", result.GetValue(3, 1).AsString());

        Assert.Null(smj.Next());
        smj.Close();
    }
}
