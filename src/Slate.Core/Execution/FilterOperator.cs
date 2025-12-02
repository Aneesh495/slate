using Slate.Core.Primitives;
using Slate.Core.Query;

namespace Slate.Core.Execution;

/// <summary>
/// Vectorized filter operator evaluating relational predicates and populating selection vectors
/// without performing expensive row-by-row data copies.
/// </summary>
public sealed class FilterOperator : PhysicalOperatorBase
{
    private readonly IPhysicalOperator _child;
    private readonly ExpressionNode _predicate;

    public override TableSchema Schema => _child.Schema;
    public override IReadOnlyList<IPhysicalOperator> Children => new[] { _child };

    public FilterOperator(IPhysicalOperator child, ExpressionNode predicate)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    }

    protected override void OnOpen() { }

    protected override VectorBatch? OnNext()
    {
        while (true)
        {
            var batch = _child.Next();
            if (batch == null)
                return null;

            if (batch.RowCount == 0)
                continue;

            int[] matchedIndices = EvaluatePredicate(batch, _predicate);
            if (matchedIndices.Length == 0)
                continue; // Skip batch if no rows matched predicate

            return new VectorBatch(Schema, batch.Columns, batch.RowCount, matchedIndices);
        }
    }

    protected override void OnClose() { }

    private int[] EvaluatePredicate(VectorBatch batch, ExpressionNode predicate)
    {
        var matched = new List<int>(batch.SelectedCount);
        int count = batch.SelectedCount;

        for (int i = 0; i < count; i++)
        {
            int actualRow = batch.SelectionVector != null ? batch.SelectionVector[i] : i;
            var val = EvaluateExpression(predicate, batch, actualRow);
            if (!val.IsNull && val.Type == DataType.Boolean && val.AsBoolean())
            {
                matched.Add(actualRow);
            }
        }

        return matched.ToArray();
    }

    private SlateValue EvaluateExpression(ExpressionNode expr, VectorBatch batch, int row)
    {
        switch (expr)
        {
            case LiteralExpression lit:
                return lit.Value;

            case ColumnReferenceExpression colRef:
                int ordinal = Schema[colRef.ColumnName].Ordinal;
                return batch.Columns[ordinal].GetValue(row);

            case BinaryExpression bin:
                if (bin.Op == BinaryOp.And)
                {
                    var l = EvaluateExpression(bin.Left, batch, row);
                    if (l.IsNull || !l.AsBoolean()) return SlateValue.FromBoolean(false);
                    var r = EvaluateExpression(bin.Right, batch, row);
                    return SlateValue.FromBoolean(!r.IsNull && r.AsBoolean());
                }
                if (bin.Op == BinaryOp.Or)
                {
                    var l = EvaluateExpression(bin.Left, batch, row);
                    if (!l.IsNull && l.AsBoolean()) return SlateValue.FromBoolean(true);
                    var r = EvaluateExpression(bin.Right, batch, row);
                    return SlateValue.FromBoolean(!r.IsNull && r.AsBoolean());
                }

                var leftVal = EvaluateExpression(bin.Left, batch, row);
                var rightVal = EvaluateExpression(bin.Right, batch, row);

                switch (bin.Op)
                {
                    case BinaryOp.Equal:
                        return SlateValue.FromBoolean(leftVal.CompareTo(rightVal) == 0);
                    case BinaryOp.NotEqual:
                        return SlateValue.FromBoolean(leftVal.CompareTo(rightVal) != 0);
                    case BinaryOp.LessThan:
                        return SlateValue.FromBoolean(leftVal.CompareTo(rightVal) < 0);
                    case BinaryOp.LessThanOrEqual:
                        return SlateValue.FromBoolean(leftVal.CompareTo(rightVal) <= 0);
                    case BinaryOp.GreaterThan:
                        return SlateValue.FromBoolean(leftVal.CompareTo(rightVal) > 0);
                    case BinaryOp.GreaterThanOrEqual:
                        return SlateValue.FromBoolean(leftVal.CompareTo(rightVal) >= 0);
                }
                break;

            case UnaryExpression un:
                var opVal = EvaluateExpression(un.Operand, batch, row);
                if (un.Op == UnaryOp.Not)
                {
                    return SlateValue.FromBoolean(!opVal.IsNull && !opVal.AsBoolean());
                }
                if (un.Op == UnaryOp.IsNull)
                {
                    return SlateValue.FromBoolean(opVal.IsNull);
                }
                if (un.Op == UnaryOp.IsNotNull)
                {
                    return SlateValue.FromBoolean(!opVal.IsNull);
                }
                break;
        }

        return SlateValue.FromBoolean(false);
    }
}
