using Slate.Core.Execution;
using Slate.Core.Primitives;
using Xunit;

namespace Slate.Tests;

public sealed class AggregateTests
{
    private (TableSchema, VectorBatch) CreateSalesData()
    {
        var schema = new TableSchema("sales", new[]
        {
            new ColumnDescriptor("dept", DataType.String, isNullable: false, ordinal: 0),
            new ColumnDescriptor("amount", DataType.Float64, isNullable: false, ordinal: 1)
        });

        var colDept = new ColumnVector(DataType.String, 5);
        var colAmount = new ColumnVector(DataType.Float64, 5);

        // Engineering: 100, 200, 300
        colDept.AppendString("Engineering"); colAmount.AppendDouble(100.0);
        colDept.AppendString("Engineering"); colAmount.AppendDouble(200.0);
        colDept.AppendString("Engineering"); colAmount.AppendDouble(300.0);

        // Marketing: 50, 150
        colDept.AppendString("Marketing"); colAmount.AppendDouble(50.0);
        colDept.AppendString("Marketing"); colAmount.AppendDouble(150.0);

        return (schema, new VectorBatch(schema, new[] { colDept, colAmount }, 5));
    }

    [Fact]
    public void AggregateOperator_GlobalAggregates_ComputesAccurateMetrics()
    {
        var (schema, batch) = CreateSalesData();
        using var scan = new TableScanOperator(schema, new[] { batch });

        var aggs = new List<AggregateSpec>
        {
            new AggregateSpec(AggregateType.Count, null, "cnt"),
            new AggregateSpec(AggregateType.Sum, "amount", "total"),
            new AggregateSpec(AggregateType.Avg, "amount", "mean"),
            new AggregateSpec(AggregateType.Min, "amount", "min_val"),
            new AggregateSpec(AggregateType.Max, "amount", "max_val")
        };

        using var aggOp = new AggregateOperator(scan, groupByColumns: null, aggs);
        var ctx = new QueryExecutionContext();
        aggOp.Open(ctx);

        var result = aggOp.Next();
        Assert.NotNull(result);
        Assert.Equal(1, result.RowCount);
        Assert.Equal(5, result.Schema.ColumnCount);

        // Count = 5
        Assert.Equal(5L, result.GetValue(0, 0).AsInt64());
        // Sum = 800.0
        Assert.Equal(800.0, result.GetValue(1, 0).AsFloat64(), precision: 2);
        // Avg = 160.0
        Assert.Equal(160.0, result.GetValue(2, 0).AsFloat64(), precision: 2);
        // Min = 50.0
        Assert.Equal(50.0, result.GetValue(3, 0).AsFloat64(), precision: 2);
        // Max = 300.0
        Assert.Equal(300.0, result.GetValue(4, 0).AsFloat64(), precision: 2);

        Assert.Null(aggOp.Next());
        aggOp.Close();
    }

    [Fact]
    public void AggregateOperator_GroupedAggregates_GroupsByDepartment()
    {
        var (schema, batch) = CreateSalesData();
        using var scan = new TableScanOperator(schema, new[] { batch });

        var aggs = new List<AggregateSpec>
        {
            new AggregateSpec(AggregateType.Count, null, "emp_count"),
            new AggregateSpec(AggregateType.Sum, "amount", "dept_sum")
        };

        using var aggOp = new AggregateOperator(scan, groupByColumns: new List<string> { "dept" }, aggs);
        var ctx = new QueryExecutionContext();
        aggOp.Open(ctx);

        var result = aggOp.Next();
        Assert.NotNull(result);
        Assert.Equal(2, result.RowCount); // Engineering and Marketing

        var dict = new Dictionary<string, (long Count, double Sum)>();
        for (int i = 0; i < result.RowCount; i++)
        {
            string dept = result.GetValue(0, i).AsString();
            long cnt = result.GetValue(1, i).AsInt64();
            double sum = result.GetValue(2, i).AsFloat64();
            dict[dept] = (cnt, sum);
        }

        Assert.Equal(3L, dict["Engineering"].Count);
        Assert.Equal(600.0, dict["Engineering"].Sum, precision: 2);

        Assert.Equal(2L, dict["Marketing"].Count);
        Assert.Equal(200.0, dict["Marketing"].Sum, precision: 2);

        Assert.Null(aggOp.Next());
        aggOp.Close();
    }
}
