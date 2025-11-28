using Slate.Core.Primitives;
using Slate.Core.Query;
using Slate.Core.Storage;

namespace Slate.Core.Execution;

/// <summary>
/// Vectorized physical table scan operator with column projection and morsel-sized batching.
/// </summary>
public sealed class TableScanOperator : PhysicalOperatorBase
{
    private readonly TableSchema _schema;
    private readonly IReadOnlyList<VectorBatch> _batches;
    private readonly List<string> _projectedColumnNames;
    private readonly ExpressionNode? _pushedPredicate;
    private int _currentBatchIndex;

    public override TableSchema Schema { get; }
    public override IReadOnlyList<IPhysicalOperator> Children => Array.Empty<IPhysicalOperator>();

    public TableScanOperator(
        TableSchema schema,
        IReadOnlyList<VectorBatch> batches,
        List<string>? projectedColumnNames = null,
        ExpressionNode? pushedPredicate = null)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _batches = batches ?? Array.Empty<VectorBatch>();
        _projectedColumnNames = projectedColumnNames ?? schema.Columns.Select(c => c.Name).ToList();
        _pushedPredicate = pushedPredicate;

        // Build projected schema
        var projCols = new List<ColumnDescriptor>();
        for (int i = 0; i < _projectedColumnNames.Count; i++)
        {
            var origCol = _schema.FindColumn(_projectedColumnNames[i])
                ?? throw new ArgumentException($"Column '{_projectedColumnNames[i]}' not found in schema.");

            projCols.Add(new ColumnDescriptor(origCol.Name, origCol.Type, origCol.IsNullable, ordinal: i, vectorDimension: origCol.VectorDimension));
        }
        Schema = new TableSchema(_schema.TableName, projCols);
    }

    protected override void OnOpen()
    {
        _currentBatchIndex = 0;
    }

    protected override VectorBatch? OnNext()
    {
        while (_currentBatchIndex < _batches.Count)
        {
            var rawBatch = _batches[_currentBatchIndex++];
            if (rawBatch.RowCount == 0)
                continue;

            // Extract projected columns
            var projectedVectors = new ColumnVector[_projectedColumnNames.Count];
            for (int i = 0; i < _projectedColumnNames.Count; i++)
            {
                string colName = _projectedColumnNames[i];
                int origOrdinal = _schema[colName].Ordinal;
                projectedVectors[i] = rawBatch.Columns[origOrdinal];
            }

            // Apply pushed predicate if present
            int[]? selection = null;
            if (_pushedPredicate != null)
            {
                selection = EvaluatePredicate(rawBatch, _pushedPredicate);
                if (selection.Length == 0)
                    continue; // All rows in this batch filtered out
            }

            return new VectorBatch(Schema, projectedVectors, rawBatch.RowCount, selection);
        }

        return null; // EOF
    }

    protected override void OnClose()
    {
        _currentBatchIndex = _batches.Count;
    }

    private int[] EvaluatePredicate(VectorBatch batch, ExpressionNode predicate)
    {
        // Simple vectorized predicate evaluation
        if (predicate is BinaryExpression bin && bin.Left is ColumnReferenceExpression colRef && bin.Right is LiteralExpression lit)
        {
            int colIdx = _schema[colRef.ColumnName].Ordinal;
            var colVec = batch.Columns[colIdx];
            var matched = new List<int>(batch.RowCount);

            for (int r = 0; r < batch.RowCount; r++)
            {
                var val = colVec.GetValue(r);
                bool satisfies = false;

                switch (bin.Op)
                {
                    case BinaryOp.Equal:
                        satisfies = val.CompareTo(lit.Value) == 0;
                        break;
                    case BinaryOp.NotEqual:
                        satisfies = val.CompareTo(lit.Value) != 0;
                        break;
                    case BinaryOp.GreaterThan:
                        satisfies = val.CompareTo(lit.Value) > 0;
                        break;
                    case BinaryOp.GreaterThanOrEqual:
                        satisfies = val.CompareTo(lit.Value) >= 0;
                        break;
                    case BinaryOp.LessThan:
                        satisfies = val.CompareTo(lit.Value) < 0;
                        break;
                    case BinaryOp.LessThanOrEqual:
                        satisfies = val.CompareTo(lit.Value) <= 0;
                        break;
                }

                if (satisfies)
                {
                    matched.Add(r);
                }
            }

            return matched.ToArray();
        }

        // Default: match all rows
        var all = new int[batch.RowCount];
        for (int i = 0; i < batch.RowCount; i++) all[i] = i;
        return all;
    }
}
