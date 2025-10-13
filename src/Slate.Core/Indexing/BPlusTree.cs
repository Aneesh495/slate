using Slate.Core.Primitives;

namespace Slate.Core.Indexing;

/// <summary>
/// A balanced B+Tree index supporting point lookups, node splitting, and fast leaf-level range scans.
/// </summary>
public sealed class BPlusTree<TKey, TValue> where TKey : IComparable<TKey>
{
    private abstract class Node
    {
        public bool IsLeaf { get; }
        public List<TKey> Keys { get; }

        protected Node(bool isLeaf, int capacity)
        {
            IsLeaf = isLeaf;
            Keys = new List<TKey>(capacity);
        }
    }

    private sealed class InternalNode : Node
    {
        public List<Node> Children { get; }

        public InternalNode(int degree) : base(false, degree)
        {
            Children = new List<Node>(degree + 1);
        }
    }

    private sealed class LeafNode : Node
    {
        public List<TValue> Values { get; }
        public LeafNode? Next { get; set; }
        public LeafNode? Prev { get; set; }

        public LeafNode(int degree) : base(true, degree)
        {
            Values = new List<TValue>(degree);
        }
    }

    private readonly int _maxDegree;
    private Node _root;
    private int _count;

    public int Count => _count;

    public BPlusTree(int maxDegree = 16)
    {
        if (maxDegree < 4) throw new ArgumentOutOfRangeException(nameof(maxDegree), "B+Tree max degree must be at least 4.");
        _maxDegree = maxDegree;
        _root = new LeafNode(maxDegree);
    }

    public void Insert(TKey key, TValue value)
    {
        if (InsertInternal(_root, key, value, out var splitKey, out var splitNode))
        {
            // Root was split, create new root
            var newRoot = new InternalNode(_maxDegree);
            newRoot.Keys.Add(splitKey!);
            newRoot.Children.Add(_root);
            newRoot.Children.Add(splitNode!);
            _root = newRoot;
        }
        _count++;
    }

    private bool InsertInternal(Node node, TKey key, TValue value, out TKey? splitKey, out Node? splitNode)
    {
        splitKey = default;
        splitNode = null;

        if (node.IsLeaf)
        {
            var leaf = (LeafNode)node;
            int idx = leaf.Keys.BinarySearch(key);
            if (idx >= 0)
            {
                leaf.Values[idx] = value;
                _count--; // Overwrite existing key
                return false;
            }

            int insertIdx = ~idx;
            leaf.Keys.Insert(insertIdx, key);
            leaf.Values.Insert(insertIdx, value);

            if (leaf.Keys.Count >= _maxDegree)
            {
                // Split leaf node
                int mid = leaf.Keys.Count / 2;
                var newLeaf = new LeafNode(_maxDegree);

                newLeaf.Keys.AddRange(leaf.Keys.Skip(mid));
                newLeaf.Values.AddRange(leaf.Values.Skip(mid));

                leaf.Keys.RemoveRange(mid, leaf.Keys.Count - mid);
                leaf.Values.RemoveRange(mid, leaf.Values.Count - mid);

                newLeaf.Next = leaf.Next;
                if (newLeaf.Next is not null) newLeaf.Next.Prev = newLeaf;
                leaf.Next = newLeaf;
                newLeaf.Prev = leaf;

                splitKey = newLeaf.Keys[0];
                splitNode = newLeaf;
                return true;
            }

            return false;
        }
        else
        {
            var internalNode = (InternalNode)node;
            int idx = internalNode.Keys.BinarySearch(key);
            int childIdx = idx >= 0 ? idx + 1 : ~idx;

            if (InsertInternal(internalNode.Children[childIdx], key, value, out var childSplitKey, out var childSplitNode))
            {
                int insertKeyIdx = internalNode.Keys.BinarySearch(childSplitKey!);
                int kIdx = insertKeyIdx >= 0 ? insertKeyIdx : ~insertKeyIdx;

                internalNode.Keys.Insert(kIdx, childSplitKey!);
                internalNode.Children.Insert(kIdx + 1, childSplitNode!);

                if (internalNode.Keys.Count >= _maxDegree)
                {
                    // Split internal node
                    int mid = internalNode.Keys.Count / 2;
                    splitKey = internalNode.Keys[mid];

                    var newInternal = new InternalNode(_maxDegree);
                    newInternal.Keys.AddRange(internalNode.Keys.Skip(mid + 1));
                    newInternal.Children.AddRange(internalNode.Children.Skip(mid + 1));

                    internalNode.Keys.RemoveRange(mid, internalNode.Keys.Count - mid);
                    internalNode.Children.RemoveRange(mid + 1, internalNode.Children.Count - (mid + 1));

                    splitNode = newInternal;
                    return true;
                }
            }

            return false;
        }
    }

    public bool TrySearch(TKey key, out TValue? value)
    {
        value = default;
        var current = _root;

        while (!current.IsLeaf)
        {
            var internalNode = (InternalNode)current;
            int idx = internalNode.Keys.BinarySearch(key);
            int childIdx = idx >= 0 ? idx + 1 : ~idx;
            current = internalNode.Children[childIdx];
        }

        var leaf = (LeafNode)current;
        int leafIdx = leaf.Keys.BinarySearch(key);
        if (leafIdx >= 0)
        {
            value = leaf.Values[leafIdx];
            return true;
        }

        return false;
    }

    public List<(TKey Key, TValue Value)> RangeScan(TKey? startKey, TKey? endKey)
    {
        var results = new List<(TKey Key, TValue Value)>();

        // Find initial leaf node
        var current = _root;
        while (!current.IsLeaf)
        {
            var internalNode = (InternalNode)current;
            int childIdx = 0;
            if (startKey is not null)
            {
                int idx = internalNode.Keys.BinarySearch(startKey);
                childIdx = idx >= 0 ? idx + 1 : ~idx;
            }
            current = internalNode.Children[childIdx];
        }

        var leaf = (LeafNode?)current;
        while (leaf is not null)
        {
            for (int i = 0; i < leaf.Keys.Count; i++)
            {
                var k = leaf.Keys[i];
                if (startKey is not null && k.CompareTo(startKey) < 0)
                    continue;
                if (endKey is not null && k.CompareTo(endKey) > 0)
                    return results;

                results.Add((k, leaf.Values[i]));
            }
            leaf = leaf.Next;
        }

        return results;
    }
}
