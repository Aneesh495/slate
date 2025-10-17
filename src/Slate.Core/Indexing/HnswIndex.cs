namespace Slate.Core.Indexing;

public sealed record KnnResult(int Id, float Distance);

/// <summary>
/// Hierarchical Navigable Small World (HNSW) vector graph index for logarithmic approximate
/// nearest neighbor (ANN) search over dense floating-point embeddings.
/// </summary>
public sealed class HnswIndex
{
    private sealed class Node
    {
        public int Id { get; }
        public float[] Vector { get; }
        public int Level { get; }
        public List<int>[] Neighbors { get; }

        public Node(int id, float[] vector, int level)
        {
            Id = id;
            Vector = vector;
            Level = level;
            Neighbors = new List<int>[level + 1];
            for (int l = 0; l <= level; l++)
            {
                Neighbors[l] = new List<int>();
            }
        }
    }

    private readonly int _m;
    private readonly int _m0;
    private readonly int _efConstruction;
    private readonly int _dimension;
    private readonly VectorMetric _metric;
    private readonly double _mL;
    private readonly Random _rng = new(42);
    private readonly Dictionary<int, Node> _nodes = new();

    private Node? _entryPoint;
    private int _maxLevel = -1;
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);

    public int Count => _nodes.Count;
    public int Dimension => _dimension;

    public HnswIndex(int dimension, VectorMetric metric = VectorMetric.Cosine, int m = 16, int efConstruction = 64)
    {
        if (dimension <= 0) throw new ArgumentOutOfRangeException(nameof(dimension));
        _dimension = dimension;
        _metric = metric;
        _m = m;
        _m0 = 2 * m;
        _efConstruction = efConstruction;
        _mL = 1.0 / Math.Log(m);
    }

    private int GenerateRandomLevel()
    {
        double r = _rng.NextDouble();
        if (r == 0) r = 0.0000001;
        int level = (int)(-Math.Log(r) * _mL);
        return level;
    }

    public void Insert(int id, float[] vector)
    {
        if (vector.Length != _dimension)
            throw new ArgumentException($"Vector dimension mismatch. Expected {_dimension}, got {vector.Length}.");

        _lock.EnterWriteLock();
        try
        {
            int insertLevel = GenerateRandomLevel();
            var newNode = new Node(id, vector, insertLevel);
            _nodes[id] = newNode;

            if (_entryPoint is null)
            {
                _entryPoint = newNode;
                _maxLevel = insertLevel;
                return;
            }

            var currObj = _entryPoint;
            float curDist = VectorDistance.ComputeDistance(vector, currObj.Vector, _metric);

            // Phase 1: Top down from maxLevel to insertLevel + 1 (greedy search)
            for (int l = _maxLevel; l > insertLevel; l--)
            {
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    foreach (int neighborId in currObj.Neighbors[l])
                    {
                        var neighbor = _nodes[neighborId];
                        float d = VectorDistance.ComputeDistance(vector, neighbor.Vector, _metric);
                        if (d < curDist)
                        {
                            curDist = d;
                            currObj = neighbor;
                            changed = true;
                        }
                    }
                }
            }

            // Phase 2: From min(maxLevel, insertLevel) down to 0
            int startLevel = Math.Min(_maxLevel, insertLevel);
            var epList = new List<Node> { currObj };

            for (int l = startLevel; l >= 0; l--)
            {
                var candidates = SearchLayer(vector, epList, _efConstruction, l);
                int maxM = l == 0 ? _m0 : _m;

                var selectedNeighbors = SelectNeighborsHeuristic(vector, candidates, maxM);
                foreach (var neighbor in selectedNeighbors)
                {
                    newNode.Neighbors[l].Add(neighbor.Id);
                    neighbor.Neighbors[l].Add(newNode.Id);

                    // Shrink connections if neighbor exceeds maxM
                    if (neighbor.Neighbors[l].Count > maxM)
                    {
                        ShrinkNeighbors(neighbor, l, maxM);
                    }
                }

                epList = candidates.Select(c => c.Node).ToList();
            }

            if (insertLevel > _maxLevel)
            {
                _maxLevel = insertLevel;
                _entryPoint = newNode;
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public List<KnnResult> KnnSearch(float[] query, int k, int ef = 0)
    {
        if (query.Length != _dimension)
            throw new ArgumentException($"Query vector dimension mismatch. Expected {_dimension}, got {query.Length}.");
        if (k <= 0) return new List<KnnResult>();

        _lock.EnterReadLock();
        try
        {
            if (_entryPoint is null) return new List<KnnResult>();

            int effectiveEf = Math.Max(k, ef > 0 ? ef : 32);
            var currObj = _entryPoint;
            float curDist = VectorDistance.ComputeDistance(query, currObj.Vector, _metric);

            // Phase 1: Fast descent from top layer to layer 1
            for (int l = _maxLevel; l > 0; l--)
            {
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    foreach (int neighborId in currObj.Neighbors[l])
                    {
                        var neighbor = _nodes[neighborId];
                        float d = VectorDistance.ComputeDistance(query, neighbor.Vector, _metric);
                        if (d < curDist)
                        {
                            curDist = d;
                            currObj = neighbor;
                            changed = true;
                        }
                    }
                }
            }

            // Phase 2: Search layer 0 with priority queue up to effectiveEf
            var candidates = SearchLayer(query, new List<Node> { currObj }, effectiveEf, 0);

            return candidates
                .OrderBy(c => c.Distance)
                .Take(k)
                .Select(c => new KnnResult(c.Node.Id, c.Distance))
                .ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private sealed record Candidate(Node Node, float Distance);

    private List<Candidate> SearchLayer(float[] query, List<Node> enterPoints, int ef, int level)
    {
        var visited = new HashSet<int>();
        var candidates = new PriorityQueue<Candidate, float>(); // Min-heap by distance
        var w = new PriorityQueue<Candidate, float>();          // Max-heap by distance (negated priority)
        var resultList = new List<Candidate>();

        foreach (var ep in enterPoints)
        {
            visited.Add(ep.Id);
            float d = VectorDistance.ComputeDistance(query, ep.Vector, _metric);
            var cand = new Candidate(ep, d);
            candidates.Enqueue(cand, d);
            w.Enqueue(cand, -d);
            resultList.Add(cand);
        }

        while (candidates.Count > 0)
        {
            var current = candidates.Dequeue();
            w.TryPeek(out _, out float maxDistPriority);
            float furthestDistance = -maxDistPriority;

            if (current.Distance > furthestDistance && w.Count >= ef)
            {
                break;
            }

            foreach (int neighborId in current.Node.Neighbors[level])
            {
                if (visited.Add(neighborId))
                {
                    var neighbor = _nodes[neighborId];
                    float d = VectorDistance.ComputeDistance(query, neighbor.Vector, _metric);

                    w.TryPeek(out _, out maxDistPriority);
                    furthestDistance = -maxDistPriority;

                    if (d < furthestDistance || w.Count < ef)
                    {
                        var cand = new Candidate(neighbor, d);
                        candidates.Enqueue(cand, d);
                        w.Enqueue(cand, -d);
                        resultList.Add(cand);

                        if (w.Count > ef)
                        {
                            var removed = w.Dequeue();
                            resultList.Remove(removed);
                        }
                    }
                }
            }
        }

        return resultList;
    }

    private List<Node> SelectNeighborsHeuristic(float[] target, List<Candidate> candidates, int maxM)
    {
        return candidates
            .OrderBy(c => c.Distance)
            .Take(maxM)
            .Select(c => c.Node)
            .ToList();
    }

    private void ShrinkNeighbors(Node node, int level, int maxM)
    {
        var currentNeighbors = node.Neighbors[level];
        if (currentNeighbors.Count <= maxM) return;

        var scored = currentNeighbors
            .Select(id => new { Id = id, Dist = VectorDistance.ComputeDistance(node.Vector, _nodes[id].Vector, _metric) })
            .OrderBy(x => x.Dist)
            .Take(maxM)
            .Select(x => x.Id)
            .ToList();

        node.Neighbors[level].Clear();
        node.Neighbors[level].AddRange(scored);
    }
}
