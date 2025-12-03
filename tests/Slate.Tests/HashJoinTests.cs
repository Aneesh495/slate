using Slate.Core.Execution;
using Slate.Core.Primitives;
using Slate.Core.Query;
using Xunit;

namespace Slate.Tests;

public sealed class HashJoinTests
{
    private (TableSchema, VectorBatch) CreateOrders()
    {
        var schema = new TableSchema("orders", new[]
        {
            new ColumnDescriptor("order_id", DataType.Int64, isNullable: false, ordinal: 0),
            new ColumnDescriptor("cust_id", DataType.Int64, isNullable: false, ordinal: 1),
            new ColumnDescriptor("amount", DataType.Float64, isNullable: false, ordinal: 2)
        });

        var colOrder = new ColumnVector(DataType.Int64, 4);
        var colCust = new ColumnVector(DataType.Int64, 4);
        var colAmount = new ColumnVector(DataType.Float64, 4);

        // Order 1 -> Cust 100
        colOrder.AppendInt64(1); colCust.AppendInt64(100); colAmount.AppendDouble(50.0);
        // Order 2 -> Cust 200
        colOrder.AppendInt64(2); colCust.AppendInt64(200); colAmount.AppendDouble(120.0);
        // Order 3 -> Cust 100 (duplicate customer)
        colOrder.AppendInt64(3); colCust.AppendInt64(100); colAmount.AppendDouble(35.0);
        // Order 4 -> Cust 999 (non-existent customer)
        colOrder.AppendInt64(4); colCust.AppendInt64(999); colAmount.AppendDouble(80.0);

        return (schema, new VectorBatch(schema, new[] { colOrder, colCust, colAmount }, 4));
    }

    private (TableSchema, VectorBatch) CreateCustomers()
    {
        var schema = new TableSchema("customers", new[]
        {
            new ColumnDescriptor("customer_id", DataType.Int64, isNullable: false, ordinal: 0),
            new ColumnDescriptor("name", DataType.String, isNullable: false, ordinal: 1)
        });

        var colId = new ColumnVector(DataType.Int64, 2);
        var colName = new ColumnVector(DataType.String, 2);

        colId.AppendInt64(100); colName.AppendString("Alice");
        colId.AppendInt64(200); colName.AppendString("Bob");

        return (schema, new VectorBatch(schema, new[] { colId, colName }, 2));
    }

    [Fact]
    public void HashJoinOperator_InnerJoin_ProducesMatchingRecords()
    {
        var (orderSchema, orderBatch) = CreateOrders();
        var (custSchema, custBatch) = CreateCustomers();

        using var probeScan = new TableScanOperator(orderSchema, new[] { orderBatch });
        using var buildScan = new TableScanOperator(custSchema, new[] { custBatch });

        using var join = new HashJoinOperator(
            probeChild: probeScan,
            buildChild: buildScan,
            probeJoinColumn: "cust_id",
            buildJoinColumn: "customer_id",
            joinType: JoinType.Inner);

        var ctx = new QueryExecutionContext();
        join.Open(ctx);

        var result = join.Next();
        Assert.NotNull(result);

        // Orders 1, 2, 3 match; Order 4 does NOT match -> 3 rows
        Assert.Equal(3, result.RowCount);
        Assert.Equal(5, result.Schema.ColumnCount); // 3 order cols + 2 cust cols

        // Row 0: Order 1 -> Alice
        Assert.Equal(1L, result.GetValue(0, 0).AsInt64());
        Assert.Equal("Alice", result.GetValue(4, 0).AsString());

        // Row 1: Order 2 -> Bob
        Assert.Equal(2L, result.GetValue(0, 1).AsInt64());
        Assert.Equal("Bob", result.GetValue(4, 1).AsString());

        // Row 2: Order 3 -> Alice
        Assert.Equal(3L, result.GetValue(0, 2).AsInt64());
        Assert.Equal("Alice", result.GetValue(4, 2).AsString());

        Assert.Null(join.Next());
        join.Close();
    }

    [Fact]
    public void HashJoinOperator_LeftJoin_PreservesUnmatchedProbeRows()
    {
        var (orderSchema, orderBatch) = CreateOrders();
        var (custSchema, custBatch) = CreateCustomers();

        using var probeScan = new TableScanOperator(orderSchema, new[] { orderBatch });
        using var buildScan = new TableScanOperator(custSchema, new[] { custBatch });

        using var join = new HashJoinOperator(
            probeChild: probeScan,
            buildChild: buildScan,
            probeJoinColumn: "cust_id",
            buildJoinColumn: "customer_id",
            joinType: JoinType.Left);

        var ctx = new QueryExecutionContext();
        join.Open(ctx);

        var result = join.Next();
        Assert.NotNull(result);

        // All 4 orders preserved
        Assert.Equal(4, result.RowCount);

        // Order 4 (row 3) has cust_id 999 -> name should be Null
        Assert.Equal(4L, result.GetValue(0, 3).AsInt64());
        Assert.True(result.GetValue(4, 3).IsNull);

        Assert.Null(join.Next());
        join.Close();
    }
}
