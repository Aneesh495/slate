using Slate.Core.Primitives;
using Slate.Core.Query;

namespace Slate.Core.Execution;

public enum AggregateType
{
    Count,
    Sum,
    Avg,
    Min,
    Max
}

public sealed record AggregateSpec(
    AggregateType Type,
    string? ArgumentColumn,
    string OutputColumnName);

internal sealed class AggregateAccumulator
{
    public long Count;
    public double Sum;
    public SlateValue Min = SlateValue.Null;
    public SlateValue Max = SlateValue.Null;

    public void Update(SlateValue val, AggregateType type)
    {
        if (type == AggregateType.Count && val.IsNull)
            return;

        Count++;

        if (val.IsNull) return;

        double num = val.Type == DataType.Float64 || val.Type == DataType.Float32 ? val.AsFloat64() : val.AsInt64();
        Sum += num;

        if (Min.IsNull || val.CompareTo(Min) < 0) Min = val;
        if (Max.IsNull || val.CompareTo(Max) > 0) Max = val;
    }

    public SlateValue Finalize(AggregateType type, DataType outputType)
    {
        switch (type)
        {
            case AggregateType.Count:
                return SlateValue.FromInt64(Count);
            case AggregateType.Sum:
                if (Count == 0) return SlateValue.Null;
                return outputType == DataType.Float64 ? SlateValue.FromFloat64(Sum) : SlateValue.FromInt64((long)Sum);
            case AggregateType.Avg:
                if (Count == 0) return SlateValue.Null;
                return SlateValue.FromFloat64(Sum / Count);
            case AggregateType.Min:
                return Min;
            case AggregateType.Max:
                return Max;
            default:
                return SlateValue.Null;
        }
    }
}

/// <summary>
/// Vectorized aggregation operator computing global or grouped aggregations (COUNT, SUM, AVG, MIN, MAX).
/// </summary>
public sealed class AggregateOperator : PhysicalOperatorBase
{
    private readonly IPhysicalOperator _child;
    private readonly List<string> _groupByColumns;
    private readonly List<AggregateSpec> _aggregates;
    private readonly int _batchSize;

    private readonly Dictionary<SlateValue, AggregateAccumulator[]> _groupedTable = new();
    private AggregateAccumulator[]? _globalAccumulators;

    private List<SlateValue[]>? _materializedResults;
    private int _cursor;

    public override TableSchema Schema { get; }
    public override IReadOnlyList<IPhysicalOperator> Children => new[] { _child };

    public AggregateOperator(
        IPhysicalOperator child,
        List<string>? groupByColumns,
        List<AggregateSpec> aggregates,
        int batchSize = 1024)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        _groupByColumns = groupByColumns ?? new List<string>();
        _aggregates = aggregates ?? throw new ArgumentNullException(nameof(aggregates));
        _batchSize = batchSize > 0 ? batchSize : 1024;

        var outCols = new List<ColumnDescriptor>();
        int ord = 0;

        foreach (var grp in _groupByColumns)
        {
            var origCol = _child.Schema[grp];
            outCols.Add(new ColumnDescriptor(grp, origCol.Type, isNullable: origCol.IsNullable, ordinal: ord++));
        }

        foreach (var agg in _aggregates)
        {
            DataType aggType = agg.Type switch
            {
                AggregateType.Count => DataType.Int64,
                AggregateType.Avg => DataType.Float64,
                _ => agg.ArgumentColumn != null ? _child.Schema[agg.ArgumentColumn].Type : DataType.Int64
            };

            outCols.Add(new ColumnDescriptor(agg.OutputColumnName, aggType, isNullable: true, ordinal: ord++));
        }

        Schema = new TableSchema("aggregated", outCols);
    }

    protected override void OnOpen()
    {
        _cursor = 0;
        _groupedTable.Clear();
        _globalAccumulators = new AggregateAccumulator[_aggregates.Count];
        for (int i = 0; i < _aggregates.Count; i++) _globalAccumulators[i] = new AggregateAccumulator();

        // Resolve argument column ordinals
        var argOrdinals = new int[_aggregates.Count];
        for (int i = 0; i < _aggregates.Count; i++)
        {
            argOrdinals[i] = _aggregates[i].ArgumentColumn != null
                ? _child.Schema[_aggregates[i].ArgumentColumn!].Ordinal
                : -1;
        }

        int groupColOrdinal = _groupByColumns.Count > 0 ? _child.Schema[_groupByColumns[0]].Ordinal : -1;

        // Drain child batches
        while (true)
        {
            var batch = _child.Next();
            if (batch == null) break;

            for (int r = 0; r < batch.SelectedCount; r++)
            {
                int actualRow = batch.SelectionVector != null ? batch.SelectionVector[r] : r;

                if (_groupByColumns.Count == 0)
                {
                    // Global aggregation
                    for (int a = 0; a < _aggregates.Count; a++)
                    {
                        var val = argOrdinals[a] >= 0 ? batch.Columns[argOrdinals[a]].GetValue(actualRow) : SlateValue.FromInt64(1);
                        _globalAccumulators[a].Update(val, _aggregates[a].Type);
                    }
                }
                else
                {
                    // Grouped aggregation
                    var groupKey = batch.Columns[groupColOrdinal].GetValue(actualRow);
                    if (!_groupedTable.TryGetValue(groupKey, out var accs))
                    {
                        accs = new AggregateAccumulator[_aggregates.Count];
                        for (int i = 0; i < _aggregates.Count; i++) accs[i] = new AggregateAccumulator();
                        _groupedTable[groupKey] = accs;
                    }

                    for (int a = 0; a < _aggregates.Count; a++)
                    {
                        var val = argOrdinals[a] >= 0 ? batch.Columns[argOrdinals[a]].GetValue(actualRow) : SlateValue.FromInt64(1);
                        accs[a].Update(val, _aggregates[a].Type);
                    }
                }
            }
        }

        // Finalize rows into _materializedResults
        _materializedResults = new List<SlateValue[]>();

        if (_groupByColumns.Count == 0)
        {
            var row = new SlateValue[_aggregates.Count];
            for (int a = 0; a < _aggregates.Count; a++)
            {
                row[a] = _globalAccumulators[a].Finalize(_aggregates[a].Type, Schema.Columns[a].Type);
            }
            _materializedResults.Add(row);
        }
        else
        {
            foreach (var kvp in _groupedTable)
            {
                var row = new SlateValue[Schema.ColumnCount];
                row[0] = kvp.Key;
                for (int a = 0; a < _aggregates.Count; a++)
                {
                    row[1 + a] = kvp.Value[a].Finalize(_aggregates[a].Type, Schema.Columns[1 + a].Type);
                }
                _materializedResults.Add(row);
            }
        }
    }

    protected override VectorBatch? OnNext()
    {
        if (_materializedResults == null || _cursor >= _materializedResults.Count)
            return null;

        int count = Math.Min(_batchSize, _materializedResults.Count - _cursor);
        var cols = new ColumnVector[Schema.ColumnCount];
        for (int c = 0; c < Schema.ColumnCount; c++)
        {
            cols[c] = new ColumnVector(Schema[c].Type, count);
        }

        for (int i = 0; i < count; i++)
        {
            var row = _materializedResults[_cursor + i];
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
        _groupedTable.Clear();
        _materializedResults = null;
        _cursor = 0;
    }
}
