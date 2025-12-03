using Slate.Core.Primitives;
using Slate.Core.Query;
using Slate.Core.Storage;

namespace Slate.Core.Execution;

public sealed class FastBloomFilter
{
    private readonly ulong[] _bits;
    private readonly int _mask;

    public FastBloomFilter(int bitCount = 1024)
    {
        _bits = new ulong[(bitCount + 63) / 64];
        _mask = bitCount - 1;
    }

    public void Add(uint hash)
    {
        uint h1 = hash;
        uint h2 = hash * 0x9E3779B1u;
        SetBit(h1);
        SetBit(h2);
    }

    public bool MayContain(uint hash)
    {
        uint h1 = hash;
        uint h2 = hash * 0x9E3779B1u;
        return CheckBit(h1) && CheckBit(h2);
    }

    private void SetBit(uint bit)
    {
        int b = (int)(bit & (uint)_mask);
        _bits[b >> 6] |= 1UL << (b & 63);
    }

    private bool CheckBit(uint bit)
    {
        int b = (int)(bit & (uint)_mask);
        return (_bits[b >> 6] & (1UL << (b & 63))) != 0;
    }
}

/// <summary>
/// Vectorized hash join operator with in-memory hash table and Bloom filter pre-filtering.
/// </summary>
public sealed class HashJoinOperator : PhysicalOperatorBase
{
    private readonly IPhysicalOperator _probeChild;
    private readonly IPhysicalOperator _buildChild;
    private readonly string _probeJoinColumn;
    private readonly string _buildJoinColumn;
    private readonly JoinType _joinType;
    private readonly int _batchSize;

    private readonly Dictionary<SlateValue, List<SlateValue[]>> _hashTable = new();
    private FastBloomFilter? _bloomFilter;
    private int _probeColOrdinal;
    private int _buildColOrdinal;

    public override TableSchema Schema { get; }
    public override IReadOnlyList<IPhysicalOperator> Children => new[] { _probeChild, _buildChild };

    public HashJoinOperator(
        IPhysicalOperator probeChild,
        IPhysicalOperator buildChild,
        string probeJoinColumn,
        string buildJoinColumn,
        JoinType joinType = JoinType.Inner,
        int batchSize = 1024)
    {
        _probeChild = probeChild ?? throw new ArgumentNullException(nameof(probeChild));
        _buildChild = buildChild ?? throw new ArgumentNullException(nameof(buildChild));
        _probeJoinColumn = probeJoinColumn ?? throw new ArgumentNullException(nameof(probeJoinColumn));
        _buildJoinColumn = buildJoinColumn ?? throw new ArgumentNullException(nameof(buildJoinColumn));
        _joinType = joinType;
        _batchSize = batchSize > 0 ? batchSize : 1024;

        // Construct concatenated joined schema: probe columns followed by build columns
        var joinedCols = new List<ColumnDescriptor>();
        int ordinal = 0;

        foreach (var col in _probeChild.Schema.Columns)
        {
            joinedCols.Add(new ColumnDescriptor(col.Name, col.Type, col.IsNullable, ordinal++, col.VectorDimension));
        }

        foreach (var col in _buildChild.Schema.Columns)
        {
            string name = _probeChild.Schema.ContainsColumn(col.Name) ? $"{_buildChild.Schema.TableName}_{col.Name}" : col.Name;
            bool isNullable = joinType == JoinType.Left ? true : col.IsNullable;
            joinedCols.Add(new ColumnDescriptor(name, col.Type, isNullable, ordinal++, col.VectorDimension));
        }

        Schema = new TableSchema("joined", joinedCols);
    }

    protected override void OnOpen()
    {
        _probeColOrdinal = _probeChild.Schema[_probeJoinColumn].Ordinal;
        _buildColOrdinal = _buildChild.Schema[_buildJoinColumn].Ordinal;
        _hashTable.Clear();

        // 1. Build Phase: Consume all batches from build side
        _bloomFilter = new FastBloomFilter(1024); // 1024 bits

        while (true)
        {
            var buildBatch = _buildChild.Next();
            if (buildBatch == null) break;

            for (int r = 0; r < buildBatch.SelectedCount; r++)
            {
                int actualRow = buildBatch.SelectionVector != null ? buildBatch.SelectionVector[r] : r;
                var joinKey = buildBatch.Columns[_buildColOrdinal].GetValue(actualRow);

                if (joinKey.IsNull) continue;

                // Add to bloom filter
                uint keyHash = (uint)joinKey.GetHashCode();
                _bloomFilter.Add(keyHash);

                // Materialize build row
                var rowValues = new SlateValue[_buildChild.Schema.ColumnCount];
                for (int c = 0; c < _buildChild.Schema.ColumnCount; c++)
                {
                    rowValues[c] = buildBatch.Columns[c].GetValue(actualRow);
                }

                if (!_hashTable.TryGetValue(joinKey, out var list))
                {
                    list = new List<SlateValue[]>();
                    _hashTable[joinKey] = list;
                }
                list.Add(rowValues);
            }
        }
    }

    protected override VectorBatch? OnNext()
    {
        // 2. Probe Phase: Stream batches from probe side and match with hash table
        while (true)
        {
            var probeBatch = _probeChild.Next();
            if (probeBatch == null) return null;

            if (probeBatch.SelectedCount == 0) continue;

            var resultRows = new List<SlateValue[]>(_batchSize);

            for (int r = 0; r < probeBatch.SelectedCount; r++)
            {
                int actualRow = probeBatch.SelectionVector != null ? probeBatch.SelectionVector[r] : r;
                var probeKey = probeBatch.Columns[_probeColOrdinal].GetValue(actualRow);

                if (probeKey.IsNull)
                {
                    if (_joinType == JoinType.Left)
                    {
                        resultRows.Add(BuildJoinedRow(probeBatch, actualRow, null));
                    }
                    continue;
                }

                // Bloom filter pre-check
                uint keyHash = (uint)probeKey.GetHashCode();
                if (_bloomFilter != null && !_bloomFilter.MayContain(keyHash))
                {
                    if (_joinType == JoinType.Left)
                    {
                        resultRows.Add(BuildJoinedRow(probeBatch, actualRow, null));
                    }
                    continue;
                }

                if (_hashTable.TryGetValue(probeKey, out var matchingBuildRows))
                {
                    foreach (var buildRow in matchingBuildRows)
                    {
                        resultRows.Add(BuildJoinedRow(probeBatch, actualRow, buildRow));
                    }
                }
                else if (_joinType == JoinType.Left)
                {
                    resultRows.Add(BuildJoinedRow(probeBatch, actualRow, null));
                }
            }

            if (resultRows.Count == 0)
                continue;

            // Transpose result rows into VectorBatch
            var outCols = new ColumnVector[Schema.ColumnCount];
            for (int c = 0; c < Schema.ColumnCount; c++)
            {
                outCols[c] = new ColumnVector(Schema[c].Type, resultRows.Count);
            }

            for (int r = 0; r < resultRows.Count; r++)
            {
                var row = resultRows[r];
                for (int c = 0; c < Schema.ColumnCount; c++)
                {
                    outCols[c].AppendValue(row[c]);
                }
            }

            return new VectorBatch(Schema, outCols, resultRows.Count);
        }
    }

    private SlateValue[] BuildJoinedRow(VectorBatch probeBatch, int probeRow, SlateValue[]? buildRow)
    {
        var row = new SlateValue[Schema.ColumnCount];
        int probeColCount = _probeChild.Schema.ColumnCount;

        for (int c = 0; c < probeColCount; c++)
        {
            row[c] = probeBatch.Columns[c].GetValue(probeRow);
        }

        int buildColCount = _buildChild.Schema.ColumnCount;
        for (int c = 0; c < buildColCount; c++)
        {
            row[probeColCount + c] = buildRow != null ? buildRow[c] : SlateValue.Null;
        }

        return row;
    }

    protected override void OnClose()
    {
        _hashTable.Clear();
        _bloomFilter = null;
    }
}
