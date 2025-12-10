using Slate.Core.Primitives;
using Slate.Core.Query;

namespace Slate.Core.Execution;

/// <summary>
/// Vectorized sort operator executing multi-column in-memory quicksort or top-k ordering.
/// Emits sorted results in columnar VectorBatch slices.
/// </summary>
public sealed class SortOperator : PhysicalOperatorBase
{
    private readonly IPhysicalOperator _child;
    private readonly List<OrderByClause> _orderBy;
    private readonly int? _limit;
    private readonly int _batchSize;

    private List<SlateValue[]>? _sortedRows;
    private int _cursor;

    public override TableSchema Schema => _child.Schema;
    public override IReadOnlyList<IPhysicalOperator> Children => new[] { _child };

    public SortOperator(
        IPhysicalOperator child,
        List<OrderByClause> orderBy,
        int? limit = null,
        int batchSize = 1024)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        _orderBy = orderBy ?? throw new ArgumentNullException(nameof(orderBy));
        _limit = limit;
        _batchSize = batchSize > 0 ? batchSize : 1024;
    }

    protected override void OnOpen()
    {
        _cursor = 0;
        var allRows = new List<SlateValue[]>();

        while (true)
        {
            var batch = _child.Next();
            if (batch == null) break;

            for (int r = 0; r < batch.SelectedCount; r++)
            {
                int actualRow = batch.SelectionVector != null ? batch.SelectionVector[r] : r;
                var row = new SlateValue[Schema.ColumnCount];
                for (int c = 0; c < Schema.ColumnCount; c++)
                {
                    row[c] = batch.Columns[c].GetValue(actualRow);
                }
                allRows.Add(row);
            }
        }

        // Resolve ordinals for sort columns
        var sortResolvers = new List<(int Ordinal, bool Ascending)>(_orderBy.Count);
        foreach (var clause in _orderBy)
        {
            if (clause.Expression is ColumnReferenceExpression colRef)
            {
                int ord = Schema[colRef.ColumnName].Ordinal;
                sortResolvers.Add((ord, clause.Ascending));
            }
        }

        allRows.Sort((r1, r2) =>
        {
            foreach (var (ord, asc) in sortResolvers)
            {
                int cmp = r1[ord].CompareTo(r2[ord]);
                if (cmp != 0)
                {
                    return asc ? cmp : -cmp;
                }
            }
            return 0;
        });

        if (_limit.HasValue && _limit.Value < allRows.Count)
        {
            _sortedRows = allRows.Take(_limit.Value).ToList();
        }
        else
        {
            _sortedRows = allRows;
        }
    }

    protected override VectorBatch? OnNext()
    {
        if (_sortedRows == null || _cursor >= _sortedRows.Count)
            return null;

        int count = Math.Min(_batchSize, _sortedRows.Count - _cursor);
        var cols = new ColumnVector[Schema.ColumnCount];
        for (int c = 0; c < Schema.ColumnCount; c++)
        {
            cols[c] = new ColumnVector(Schema[c].Type, count);
        }

        for (int i = 0; i < count; i++)
        {
            var row = _sortedRows[_cursor + i];
            for (int c = 0; c < Schema.ColumnCount; c++)
            {
                cols[c].AppendValue(row[c]);
            }
        }

        _cursor += count;
        return new VectorBatch(Schema, cols, count);
    }

    protected override void OnClose()
    {
        _sortedRows = null;
        _cursor = 0;
    }
}
