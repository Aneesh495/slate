using Slate.Core.Execution;
using Slate.Core.Primitives;
using Xunit;

namespace Slate.Tests;

public sealed class PhysicalPlanTests
{
    private sealed class MockBatchOperator : PhysicalOperatorBase
    {
        private readonly TableSchema _schema;
        private readonly int _totalBatches;
        private readonly int _rowsPerBatch;
        private int _producedBatches;

        public override TableSchema Schema => _schema;
        public override IReadOnlyList<IPhysicalOperator> Children => Array.Empty<IPhysicalOperator>();

        public MockBatchOperator(TableSchema schema, int totalBatches, int rowsPerBatch)
        {
            _schema = schema;
            _totalBatches = totalBatches;
            _rowsPerBatch = rowsPerBatch;
        }

        protected override void OnOpen()
        {
            _producedBatches = 0;
        }

        protected override VectorBatch? OnNext()
        {
            if (_producedBatches >= _totalBatches)
                return null;

            _producedBatches++;
            var col = new ColumnVector(DataType.Int64, _rowsPerBatch);
            for (int i = 0; i < _rowsPerBatch; i++)
            {
                col.AppendInt64(i);
            }

            return new VectorBatch(_schema, new[] { col }, _rowsPerBatch);
        }

        protected override void OnClose() { }
    }

    [Fact]
    public void PhysicalOperator_Lifecycle_ExecutesAndTracksMetrics()
    {
        var schema = new TableSchema("test", new[] { new ColumnDescriptor("v", DataType.Int64, isNullable: false, ordinal: 0) });
        using var op = new MockBatchOperator(schema, totalBatches: 3, rowsPerBatch: 100);

        var ctx = new QueryExecutionContext();
        op.Open(ctx);
        Assert.True(op.IsOpen);

        int batchesCount = 0;
        while (op.Next() is not null)
        {
            batchesCount++;
        }

        Assert.Equal(3, batchesCount);
        Assert.Equal(300, op.RowsProduced);
        Assert.True(op.ExecutionTimeNs >= 0);

        op.Close();
        Assert.False(op.IsOpen);
    }

    [Fact]
    public void PhysicalOperator_Cancellation_ThrowsOperationCanceledException()
    {
        var schema = new TableSchema("test", new[] { new ColumnDescriptor("v", DataType.Int64, isNullable: false, ordinal: 0) });
        using var op = new MockBatchOperator(schema, totalBatches: 10, rowsPerBatch: 50);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel

        var ctx = new QueryExecutionContext(cancellationToken: cts.Token);
        op.Open(ctx);

        Assert.Throws<OperationCanceledException>(() => op.Next());
    }
}
