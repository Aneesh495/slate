using Slate.Core.Indexing;
using Slate.Core.Primitives;

namespace Slate.Core.Execution;

/// <summary>
/// Vectorized physical index scan operator leveraging secondary B+Tree indexes
/// to bypass full table scans for point lookups and range scans.
/// </summary>
public sealed class IndexScanOperator : PhysicalOperatorBase
{
    private readonly TableSchema _schema;
    private readonly BPlusTree<SlateValue, SlateValue[]> _index;
    private readonly SlateValue? _startKey;
    private readonly SlateValue? _endKey;
    private readonly bool _isPointLookup;
    private readonly int _batchSize;

    private List<(SlateValue Key, SlateValue[] Values)>? _matchedRecords;
    private int _cursor;

    public override TableSchema Schema => _schema;
    public override IReadOnlyList<IPhysicalOperator> Children => Array.Empty<IPhysicalOperator>();

    public IndexScanOperator(
        TableSchema schema,
        BPlusTree<SlateValue, SlateValue[]> index,
        SlateValue? pointLookupKey = null,
        SlateValue? startKey = null,
        SlateValue? endKey = null,
        int batchSize = 1024)
    {
        _schema = schema ?? throw new ArgumentNullException(nameof(schema));
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _batchSize = batchSize > 0 ? batchSize : 1024;

        if (pointLookupKey.HasValue)
        {
            _isPointLookup = true;
            _startKey = pointLookupKey.Value;
            _endKey = pointLookupKey.Value;
        }
        else
        {
            _isPointLookup = false;
            _startKey = startKey;
            _endKey = endKey;
        }
    }

    protected override void OnOpen()
    {
        _cursor = 0;
        _matchedRecords = new List<(SlateValue Key, SlateValue[] Values)>();

        if (_isPointLookup && _startKey.HasValue)
        {
            if (_index.TrySearch(_startKey.Value, out var values) && values is not null)
            {
                _matchedRecords.Add((_startKey.Value, values));
            }
        }
        else
        {
            _matchedRecords = _index.RangeScanBounded(_startKey ?? default, _endKey ?? default, _startKey.HasValue, _endKey.HasValue);
        }
    }

    protected override VectorBatch? OnNext()
    {
        if (_matchedRecords == null || _cursor >= _matchedRecords.Count)
            return null;

        int count = Math.Min(_batchSize, _matchedRecords.Count - _cursor);
        var cols = new ColumnVector[_schema.ColumnCount];
        for (int c = 0; c < _schema.ColumnCount; c++)
        {
            cols[c] = new ColumnVector(_schema[c].Type, count);
        }

        for (int i = 0; i < count; i++)
        {
            var record = _matchedRecords[_cursor + i];
            for (int c = 0; c < _schema.ColumnCount; c++)
            {
                var val = c < record.Values.Length ? record.Values[c] : SlateValue.Null;
                cols[c].AppendValue(val);
            }
        }

        _cursor += count;
        return new VectorBatch(_schema, cols, count);
    }

    protected override void OnClose()
    {
        _matchedRecords = null;
        _cursor = 0;
    }
}
