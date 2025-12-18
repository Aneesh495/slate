using Slate.Core.Indexing;
using Slate.Core.Primitives;
using Slate.Core.Query;

namespace Slate.Core.Execution;

/// <summary>
/// Physical operator executing Vector Top-K Nearest Neighbor (KNN) search.
/// Dynamically accelerates queries via HNSW vector graphs when available,
/// or executes AVX2/NEON SIMD brute-force linear scans.
/// </summary>
public sealed class VectorKnnOperator : PhysicalOperatorBase
{
    private readonly IPhysicalOperator? _child;
    private readonly HnswIndex? _hnswIndex;
    private readonly string _vectorColumn;
    private readonly VectorDistanceMetric _metric;
    private readonly float[] _targetVector;
    private readonly int _k;
    private readonly int _vectorColOrdinal;

    private List<(SlateValue[] Row, float Distance)>? _topKResults;
    private int _cursor;

    public override TableSchema Schema { get; }
    public override IReadOnlyList<IPhysicalOperator> Children => _child != null ? new[] { _child } : Array.Empty<IPhysicalOperator>();

    // Brute-force scan constructor
    public VectorKnnOperator(
        IPhysicalOperator child,
        string vectorColumn,
        VectorDistanceMetric metric,
        float[] targetVector,
        int k)
    {
        _child = child ?? throw new ArgumentNullException(nameof(child));
        _hnswIndex = null;
        _vectorColumn = vectorColumn ?? throw new ArgumentNullException(nameof(vectorColumn));
        _metric = metric;
        _targetVector = targetVector ?? throw new ArgumentNullException(nameof(targetVector));
        _k = k > 0 ? k : 10;
        _vectorColOrdinal = _child.Schema[_vectorColumn].Ordinal;

        // Output schema: all child columns + distance column
        var outCols = new List<ColumnDescriptor>(_child.Schema.Columns);
        outCols.Add(new ColumnDescriptor("distance", DataType.Float32, isNullable: false, ordinal: outCols.Count));
        Schema = new TableSchema(_child.Schema.TableName, outCols);
    }

    // HNSW index-accelerated constructor
    public VectorKnnOperator(
        HnswIndex hnswIndex,
        TableSchema baseSchema,
        string vectorColumn,
        VectorDistanceMetric metric,
        float[] targetVector,
        int k)
    {
        _child = null;
        _hnswIndex = hnswIndex ?? throw new ArgumentNullException(nameof(hnswIndex));
        _vectorColumn = vectorColumn ?? throw new ArgumentNullException(nameof(vectorColumn));
        _metric = metric;
        _targetVector = targetVector ?? throw new ArgumentNullException(nameof(targetVector));
        _k = k > 0 ? k : 10;
        _vectorColOrdinal = baseSchema[_vectorColumn].Ordinal;

        var outCols = new List<ColumnDescriptor>(baseSchema.Columns);
        outCols.Add(new ColumnDescriptor("distance", DataType.Float32, isNullable: false, ordinal: outCols.Count));
        Schema = new TableSchema(baseSchema.TableName, outCols);
    }

    protected override void OnOpen()
    {
        _cursor = 0;
        var heap = new PriorityQueue<SlateValue[], float>(Comparer<float>.Create((a, b) => b.CompareTo(a))); // Max-heap

        if (_hnswIndex != null)
        {
            // Accelerated HNSW search
            var neighbors = _hnswIndex.KnnSearch(_targetVector, _k);
            _topKResults = new List<(SlateValue[] Row, float Distance)>(neighbors.Count);

            foreach (var res in neighbors)
            {
                var vec = _hnswIndex.GetVector(res.Id);
                var row = new SlateValue[Schema.ColumnCount - 1];
                row[0] = SlateValue.FromInt64(res.Id);
                if (_vectorColOrdinal < row.Length && vec != null)
                {
                    row[_vectorColOrdinal] = SlateValue.FromVector(vec);
                }
                _topKResults.Add((row, res.Distance));
            }

            _topKResults.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return;
        }

        // SIMD Brute-force scan over child batches
        var scoredRows = new List<(SlateValue[] Row, float Distance)>();

        while (_child != null)
        {
            var batch = _child.Next();
            if (batch == null) break;

            for (int r = 0; r < batch.SelectedCount; r++)
            {
                int actualRow = batch.SelectionVector != null ? batch.SelectionVector[r] : r;
                var vecVal = batch.Columns[_vectorColOrdinal].GetValue(actualRow);
                if (vecVal.IsNull) continue;

                float[] rowVec = vecVal.AsVector();
                float dist = ComputeDistance(rowVec, _targetVector, _metric);

                var row = new SlateValue[_child.Schema.ColumnCount];
                for (int c = 0; c < _child.Schema.ColumnCount; c++)
                {
                    row[c] = batch.Columns[c].GetValue(actualRow);
                }

                scoredRows.Add((row, dist));
            }
        }

        scoredRows.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        _topKResults = scoredRows.Take(_k).ToList();
    }

    private static float ComputeDistance(float[] a, float[] b, VectorDistanceMetric metric) => metric switch
    {
        VectorDistanceMetric.L2 => MathF.Sqrt(VectorDistance.EuclideanDistanceSquared(a, b)),
        VectorDistanceMetric.DotProduct => -VectorDistance.DotProduct(a, b),
        VectorDistanceMetric.Cosine => VectorDistance.CosineDistance(a, b),
        _ => MathF.Sqrt(VectorDistance.EuclideanDistanceSquared(a, b))
    };

    protected override VectorBatch? OnNext()
    {
        if (_topKResults == null || _cursor >= _topKResults.Count)
            return null;

        int count = Math.Min(1024, _topKResults.Count - _cursor);
        var cols = new ColumnVector[Schema.ColumnCount];
        for (int c = 0; c < Schema.ColumnCount; c++)
        {
            cols[c] = new ColumnVector(Schema[c].Type, count);
        }

        for (int i = 0; i < count; i++)
        {
            var (row, dist) = _topKResults[_cursor + i];
            for (int c = 0; c < row.Length; c++)
            {
                cols[c].AppendValue(row[c]);
            }
            // Append distance column
            cols[^1].AppendDouble(dist);
        }

        _cursor += count;
        return new VectorBatch(Schema, cols, count);
    }

    protected override void OnClose()
    {
        _topKResults = null;
        _cursor = 0;
    }
}
