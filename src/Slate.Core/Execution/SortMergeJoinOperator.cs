using Slate.Core.Primitives;
using Slate.Core.Query;

namespace Slate.Core.Execution;

/// <summary>
/// Vectorized sort-merge join operator joining two pre-sorted inputs in linear time.
/// </summary>
public sealed class SortMergeJoinOperator : PhysicalOperatorBase
{
    private readonly IPhysicalOperator _leftChild;
    private readonly IPhysicalOperator _rightChild;
    private readonly string _leftJoinColumn;
    private readonly string _rightJoinColumn;
    private readonly JoinType _joinType;
    private readonly int _batchSize;

    private int _leftColOrdinal;
    private int _rightColOrdinal;

    private List<SlateValue[]> _leftRows = new();
    private List<SlateValue[]> _rightRows = new();
    private List<SlateValue[]>? _joinedRows;
    private int _cursor;

    public override TableSchema Schema { get; }
    public override IReadOnlyList<IPhysicalOperator> Children => new[] { _leftChild, _rightChild };

    public SortMergeJoinOperator(
        IPhysicalOperator leftChild,
        IPhysicalOperator rightChild,
        string leftJoinColumn,
        string rightJoinColumn,
        JoinType joinType = JoinType.Inner,
        int batchSize = 1024)
    {
        _leftChild = leftChild ?? throw new ArgumentNullException(nameof(leftChild));
        _rightChild = rightChild ?? throw new ArgumentNullException(nameof(rightChild));
        _leftJoinColumn = leftJoinColumn ?? throw new ArgumentNullException(nameof(leftJoinColumn));
        _rightJoinColumn = rightJoinColumn ?? throw new ArgumentNullException(nameof(rightJoinColumn));
        _joinType = joinType;
        _batchSize = batchSize > 0 ? batchSize : 1024;

        var joinedCols = new List<ColumnDescriptor>();
        int ordinal = 0;

        foreach (var col in _leftChild.Schema.Columns)
        {
            joinedCols.Add(new ColumnDescriptor(col.Name, col.Type, col.IsNullable, ordinal++, col.VectorDimension));
        }

        foreach (var col in _rightChild.Schema.Columns)
        {
            string name = _leftChild.Schema.ContainsColumn(col.Name) ? $"{_rightChild.Schema.TableName}_{col.Name}" : col.Name;
            bool isNullable = joinType == JoinType.Left ? true : col.IsNullable;
            joinedCols.Add(new ColumnDescriptor(name, col.Type, isNullable, ordinal++, col.VectorDimension));
        }

        Schema = new TableSchema("sort_merged", joinedCols);
    }

    protected override void OnOpen()
    {
        _leftColOrdinal = _leftChild.Schema[_leftJoinColumn].Ordinal;
        _rightColOrdinal = _rightChild.Schema[_rightJoinColumn].Ordinal;
        _cursor = 0;

        // Drain left and right children into sorted lists
        _leftRows = DrainChild(_leftChild);
        _rightRows = DrainChild(_rightChild);
        _joinedRows = new List<SlateValue[]>();

        int leftIdx = 0;
        int rightIdx = 0;

        while (leftIdx < _leftRows.Count && rightIdx < _rightRows.Count)
        {
            var leftKey = _leftRows[leftIdx][_leftColOrdinal];
            var rightKey = _rightRows[rightIdx][_rightColOrdinal];

            int cmp = leftKey.CompareTo(rightKey);

            if (cmp < 0)
            {
                if (_joinType == JoinType.Left)
                {
                    _joinedRows.Add(MergeRow(_leftRows[leftIdx], null));
                }
                leftIdx++;
            }
            else if (cmp > 0)
            {
                rightIdx++;
            }
            else
            {
                // Keys match: handle duplicates on right side
                int matchRightStart = rightIdx;
                while (matchRightStart < _rightRows.Count && _rightRows[matchRightStart][_rightColOrdinal].CompareTo(leftKey) == 0)
                {
                    matchRightStart++;
                }

                // For each duplicate on left side with this key
                int currLeft = leftIdx;
                while (currLeft < _leftRows.Count && _leftRows[currLeft][_leftColOrdinal].CompareTo(leftKey) == 0)
                {
                    for (int r = rightIdx; r < matchRightStart; r++)
                    {
                        _joinedRows.Add(MergeRow(_leftRows[currLeft], _rightRows[r]));
                    }
                    currLeft++;
                }

                leftIdx = currLeft;
                rightIdx = matchRightStart;
            }
        }

        if (_joinType == JoinType.Left)
        {
            while (leftIdx < _leftRows.Count)
            {
                _joinedRows.Add(MergeRow(_leftRows[leftIdx], null));
                leftIdx++;
            }
        }
    }

    private List<SlateValue[]> DrainChild(IPhysicalOperator child)
    {
        var list = new List<SlateValue[]>();
        while (true)
        {
            var batch = child.Next();
            if (batch == null) break;

            for (int r = 0; r < batch.SelectedCount; r++)
            {
                int actualRow = batch.SelectionVector != null ? batch.SelectionVector[r] : r;
                var row = new SlateValue[child.Schema.ColumnCount];
                for (int c = 0; c < child.Schema.ColumnCount; c++)
                {
                    row[c] = batch.Columns[c].GetValue(actualRow);
                }
                list.Add(row);
            }
        }
        return list;
    }

    private SlateValue[] MergeRow(SlateValue[] leftRow, SlateValue[]? rightRow)
    {
        var merged = new SlateValue[Schema.ColumnCount];
        int leftLen = _leftChild.Schema.ColumnCount;
        int rightLen = _rightChild.Schema.ColumnCount;

        for (int i = 0; i < leftLen; i++) merged[i] = leftRow[i];
        for (int i = 0; i < rightLen; i++) merged[leftLen + i] = rightRow != null ? rightRow[i] : SlateValue.Null;

        return merged;
    }

    protected override VectorBatch? OnNext()
    {
        if (_joinedRows == null || _cursor >= _joinedRows.Count)
            return null;

        int count = Math.Min(_batchSize, _joinedRows.Count - _cursor);
        var cols = new ColumnVector[Schema.ColumnCount];
        for (int c = 0; c < Schema.ColumnCount; c++)
        {
            cols[c] = new ColumnVector(Schema[c].Type, count);
        }

        for (int i = 0; i < count; i++)
        {
            var row = _joinedRows[_cursor + i];
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
        _leftRows.Clear();
        _rightRows.Clear();
        _joinedRows = null;
        _cursor = 0;
    }
}
