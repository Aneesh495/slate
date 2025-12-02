using Slate.Core.Execution;
using Slate.Core.Primitives;
using Slate.Core.Query;
using Xunit;

namespace Slate.Tests;

public sealed class FilterProjectTests
{
    private TableSchema CreateSchema() => new(
        "numbers",
        new[]
        {
            new ColumnDescriptor("x", DataType.Int64, isNullable: false, ordinal: 0),
            new ColumnDescriptor("y", DataType.Int64, isNullable: false, ordinal: 1)
        },
        primaryKeyName: "x");

    private VectorBatch CreateBatch(TableSchema schema, int start, int count)
    {
        var colX = new ColumnVector(DataType.Int64, count);
        var colY = new ColumnVector(DataType.Int64, count);

        for (int i = 0; i < count; i++)
        {
            colX.AppendInt64(start + i);
            colY.AppendInt64((start + i) * 2);
        }

        return new VectorBatch(schema, new[] { colX, colY }, count);
    }

    [Fact]
    public void FilterOperator_CompoundPredicate_FiltersMatchingRows()
    {
        var schema = CreateSchema();
        var batch = CreateBatch(schema, 1, 100);

        using var scan = new TableScanOperator(schema, new[] { batch });

        // Predicate: x >= 20 AND x <= 30
        var pred = new BinaryExpression(
            new BinaryExpression(new ColumnReferenceExpression(null, "x"), BinaryOp.GreaterThanOrEqual, new LiteralExpression(SlateValue.FromInt64(20))),
            BinaryOp.And,
            new BinaryExpression(new ColumnReferenceExpression(null, "x"), BinaryOp.LessThanOrEqual, new LiteralExpression(SlateValue.FromInt64(30)))
        );

        using var filter = new FilterOperator(scan, pred);
        var ctx = new QueryExecutionContext();
        filter.Open(ctx);

        var filteredBatch = filter.Next();
        Assert.NotNull(filteredBatch);
        Assert.Equal(11, filteredBatch.SelectedCount); // 20 through 30 = 11 rows
        Assert.Equal(20L, filteredBatch.GetValue(0, 0).AsInt64());
        Assert.Equal(30L, filteredBatch.GetValue(0, 10).AsInt64());

        Assert.Null(filter.Next());
        filter.Close();
    }

    [Fact]
    public void ProjectOperator_ArithmeticExpressions_CalculatesCorrectValues()
    {
        var schema = CreateSchema();
        var batch = CreateBatch(schema, 10, 5); // x in [10, 11, 12, 13, 14]

        using var scan = new TableScanOperator(schema, new[] { batch });

        // Project: x_times_ten = x * 10, x_plus_y = x + y
        var projSchema = new TableSchema("proj", new[]
        {
            new ColumnDescriptor("x_times_ten", DataType.Int64, isNullable: true, ordinal: 0),
            new ColumnDescriptor("x_plus_y", DataType.Int64, isNullable: true, ordinal: 1)
        });

        var projs = new List<SelectItem>
        {
            new SelectItem(new BinaryExpression(new ColumnReferenceExpression(null, "x"), BinaryOp.Multiply, new LiteralExpression(SlateValue.FromInt64(10))), "x_times_ten"),
            new SelectItem(new BinaryExpression(new ColumnReferenceExpression(null, "x"), BinaryOp.Add, new ColumnReferenceExpression(null, "y")), "x_plus_y")
        };

        using var project = new ProjectOperator(scan, projs, projSchema);
        var ctx = new QueryExecutionContext();
        project.Open(ctx);

        var result = project.Next();
        Assert.NotNull(result);
        Assert.Equal(5, result.RowCount);

        // Row 0: x=10, y=20 -> x*10 = 100, x+y = 30
        Assert.Equal(100L, result.GetValue(0, 0).AsInt64());
        Assert.Equal(30L, result.GetValue(1, 0).AsInt64());

        // Row 4: x=14, y=28 -> x*10 = 140, x+y = 42
        Assert.Equal(140L, result.GetValue(0, 4).AsInt64());
        Assert.Equal(42L, result.GetValue(1, 4).AsInt64());

        Assert.Null(project.Next());
        project.Close();
    }
}
