using Slate.Core.Primitives;
using Slate.Core.Query;

namespace Slate.Core.Execution;

/// <summary>
/// Vectorized projection operator calculating scalar expressions and renaming output columns.
/// </summary>
public sealed class ProjectOperator : PhysicalOperatorBase
{
    private readonly IPhysicalOperator _child;
    private readonly List<SelectItem> _projections;

    public override TableSchema Schema { get; }
    public override IReadOnlyList<IPhysicalOperator> Children => new[] { _child };

    public ProjectOperator(IPhysicalOperator child, List<SelectItem> projections, TableSchema projectedSchema)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        _projections = projections ?? throw new ArgumentNullException(nameof(projections));
        Schema = projectedSchema ?? throw new ArgumentNullException(nameof(projectedSchema));
    }

    protected override void OnOpen() { }

    protected override VectorBatch? OnNext()
    {
        var inBatch = _child.Next();
        if (inBatch == null)
            return null;

        int rowCount = inBatch.SelectedCount;
        var outColumns = new ColumnVector[Schema.ColumnCount];

        for (int c = 0; c < Schema.ColumnCount; c++)
        {
            outColumns[c] = new ColumnVector(Schema[c].Type, rowCount);
        }

        for (int i = 0; i < rowCount; i++)
        {
            int actualRow = inBatch.SelectionVector != null ? inBatch.SelectionVector[i] : i;

            for (int c = 0; c < _projections.Count; c++)
            {
                var val = EvaluateExpression(_projections[c].Expression, inBatch, actualRow);
                outColumns[c].AppendValue(val);
            }
        }

        return new VectorBatch(Schema, outColumns, rowCount);
    }

    protected override void OnClose() { }

    private SlateValue EvaluateExpression(ExpressionNode expr, VectorBatch batch, int row)
    {
        switch (expr)
        {
            case LiteralExpression lit:
                return lit.Value;

            case ColumnReferenceExpression colRef:
                int ordinal = _child.Schema[colRef.ColumnName].Ordinal;
                return batch.Columns[ordinal].GetValue(row);

            case BinaryExpression bin:
                var lVal = EvaluateExpression(bin.Left, batch, row);
                var rVal = EvaluateExpression(bin.Right, batch, row);

                if (lVal.IsNull || rVal.IsNull)
                    return SlateValue.Null;

                switch (bin.Op)
                {
                    case BinaryOp.Add:
                        if (lVal.Type == DataType.Float64 || rVal.Type == DataType.Float64)
                            return SlateValue.FromFloat64(lVal.AsFloat64() + rVal.AsFloat64());
                        return SlateValue.FromInt64(lVal.AsInt64() + rVal.AsInt64());

                    case BinaryOp.Subtract:
                        if (lVal.Type == DataType.Float64 || rVal.Type == DataType.Float64)
                            return SlateValue.FromFloat64(lVal.AsFloat64() - rVal.AsFloat64());
                        return SlateValue.FromInt64(lVal.AsInt64() - rVal.AsInt64());

                    case BinaryOp.Multiply:
                        if (lVal.Type == DataType.Float64 || rVal.Type == DataType.Float64)
                            return SlateValue.FromFloat64(lVal.AsFloat64() * rVal.AsFloat64());
                        return SlateValue.FromInt64(lVal.AsInt64() * rVal.AsInt64());

                    case BinaryOp.Divide:
                        double divisor = rVal.AsFloat64();
                        if (divisor == 0.0) return SlateValue.Null;
                        if (lVal.Type == DataType.Float64 || rVal.Type == DataType.Float64)
                            return SlateValue.FromFloat64(lVal.AsFloat64() / divisor);
                        return SlateValue.FromInt64(lVal.AsInt64() / (long)divisor);
                }
                break;
        }

        return SlateValue.Null;
    }
}
