namespace Slate.Core.Indexing;

/// <summary>
/// Adaptive Radix Tree (ART) implementation featuring dynamic node transitions
/// (Node4 -> Node16 -> Node48 -> Node256) and path compression for lexicographical prefix/range indexing.
/// </summary>
public sealed class AdaptiveRadixTree
{
    private abstract class ArtNode
    {
        public byte[] Prefix = Array.Empty<byte>();
        public int PrefixLen;

        public abstract bool TryFindChild(byte b, out ArtNode? child);
        public abstract ArtNode AddOrUpdateChild(byte b, ArtNode child);
        public abstract void CollectAll(List<(byte[] Key, ulong Value)> output, List<byte> currentKey);
    }

    private sealed class ArtLeaf : ArtNode
    {
        public byte[] Key;
        public ulong Value;

        public ArtLeaf(byte[] key, ulong value)
        {
            Key = key;
            Value = value;
        }

        public override bool TryFindChild(byte b, out ArtNode? child)
        {
            child = null;
            return false;
        }

        public override ArtNode AddOrUpdateChild(byte b, ArtNode child) => throw new InvalidOperationException("Cannot add child to leaf.");

        public override void CollectAll(List<(byte[] Key, ulong Value)> output, List<byte> currentKey)
        {
            output.Add((Key, Value));
        }
    }

    private sealed class Node4 : ArtNode
    {
        public byte[] Keys = new byte[4];
        public ArtNode[] Children = new ArtNode[4];
        public byte ChildCount;

        public override bool TryFindChild(byte b, out ArtNode? child)
        {
            for (int i = 0; i < ChildCount; i++)
            {
                if (Keys[i] == b)
                {
                    child = Children[i];
                    return true;
                }
            }
            child = null;
            return false;
        }

        public override ArtNode AddOrUpdateChild(byte b, ArtNode child)
        {
            for (int i = 0; i < ChildCount; i++)
            {
                if (Keys[i] == b)
                {
                    Children[i] = child;
                    return this;
                }
            }

            if (ChildCount < 4)
            {
                int insertIdx = 0;
                while (insertIdx < ChildCount && Keys[insertIdx] < b) insertIdx++;
                for (int i = ChildCount; i > insertIdx; i--)
                {
                    Keys[i] = Keys[i - 1];
                    Children[i] = Children[i - 1];
                }
                Keys[insertIdx] = b;
                Children[insertIdx] = child;
                ChildCount++;
                return this;
            }

            // Upgrade to Node16
            var n16 = new Node16 { Prefix = Prefix, PrefixLen = PrefixLen, ChildCount = 4 };
            Array.Copy(Keys, n16.Keys, 4);
            Array.Copy(Children, n16.Children, 4);
            return n16.AddOrUpdateChild(b, child);
        }

        public override void CollectAll(List<(byte[] Key, ulong Value)> output, List<byte> currentKey)
        {
            for (int i = 0; i < ChildCount; i++)
            {
                currentKey.Add(Keys[i]);
                Children[i].CollectAll(output, currentKey);
                currentKey.RemoveAt(currentKey.Count - 1);
            }
        }
    }

    private sealed class Node16 : ArtNode
    {
        public byte[] Keys = new byte[16];
        public ArtNode[] Children = new ArtNode[16];
        public byte ChildCount;

        public override bool TryFindChild(byte b, out ArtNode? child)
        {
            for (int i = 0; i < ChildCount; i++)
            {
                if (Keys[i] == b)
                {
                    child = Children[i];
                    return true;
                }
            }
            child = null;
            return false;
        }

        public override ArtNode AddOrUpdateChild(byte b, ArtNode child)
        {
            for (int i = 0; i < ChildCount; i++)
            {
                if (Keys[i] == b)
                {
                    Children[i] = child;
                    return this;
                }
            }

            if (ChildCount < 16)
            {
                int insertIdx = 0;
                while (insertIdx < ChildCount && Keys[insertIdx] < b) insertIdx++;
                for (int i = ChildCount; i > insertIdx; i--)
                {
                    Keys[i] = Keys[i - 1];
                    Children[i] = Children[i - 1];
                }
                Keys[insertIdx] = b;
                Children[insertIdx] = child;
                ChildCount++;
                return this;
            }

            // Upgrade to Node48
            var n48 = new Node48 { Prefix = Prefix, PrefixLen = PrefixLen, ChildCount = 16 };
            for (int i = 0; i < 16; i++)
            {
                n48.ChildPointers[i] = Children[i];
                n48.KeyIndexes[Keys[i]] = (byte)(i + 1);
            }
            return n48.AddOrUpdateChild(b, child);
        }

        public override void CollectAll(List<(byte[] Key, ulong Value)> output, List<byte> currentKey)
        {
            for (int i = 0; i < ChildCount; i++)
            {
                currentKey.Add(Keys[i]);
                Children[i].CollectAll(output, currentKey);
                currentKey.RemoveAt(currentKey.Count - 1);
            }
        }
    }

    private sealed class Node48 : ArtNode
    {
        public byte[] KeyIndexes = new byte[256];
        public ArtNode[] ChildPointers = new ArtNode[48];
        public byte ChildCount;

        public override bool TryFindChild(byte b, out ArtNode? child)
        {
            byte idx = KeyIndexes[b];
            if (idx != 0)
            {
                child = ChildPointers[idx - 1];
                return true;
            }
            child = null;
            return false;
        }

        public override ArtNode AddOrUpdateChild(byte b, ArtNode child)
        {
            byte existingIdx = KeyIndexes[b];
            if (existingIdx != 0)
            {
                ChildPointers[existingIdx - 1] = child;
                return this;
            }

            if (ChildCount < 48)
            {
                int emptySlot = 0;
                while (ChildPointers[emptySlot] is not null) emptySlot++;
                ChildPointers[emptySlot] = child;
                KeyIndexes[b] = (byte)(emptySlot + 1);
                ChildCount++;
                return this;
            }

            // Upgrade to Node256
            var n256 = new Node256 { Prefix = Prefix, PrefixLen = PrefixLen, ChildCount = 48 };
            for (int i = 0; i < 256; i++)
            {
                byte idx = KeyIndexes[i];
                if (idx != 0)
                {
                    n256.Children[i] = ChildPointers[idx - 1];
                }
            }
            return n256.AddOrUpdateChild(b, child);
        }

        public override void CollectAll(List<(byte[] Key, ulong Value)> output, List<byte> currentKey)
        {
            for (int i = 0; i < 256; i++)
            {
                byte idx = KeyIndexes[i];
                if (idx != 0)
                {
                    currentKey.Add((byte)i);
                    ChildPointers[idx - 1].CollectAll(output, currentKey);
                    currentKey.RemoveAt(currentKey.Count - 1);
                }
            }
        }
    }

    private sealed class Node256 : ArtNode
    {
        public ArtNode?[] Children = new ArtNode?[256];
        public int ChildCount;

        public override bool TryFindChild(byte b, out ArtNode? child)
        {
            child = Children[b];
            return child is not null;
        }

        public override ArtNode AddOrUpdateChild(byte b, ArtNode child)
        {
            if (Children[b] is null)
            {
                Children[b] = child;
                ChildCount++;
            }
            else
            {
                Children[b] = child;
            }
            return this;
        }

        public override void CollectAll(List<(byte[] Key, ulong Value)> output, List<byte> currentKey)
        {
            for (int i = 0; i < 256; i++)
            {
                if (Children[i] is not null)
                {
                    currentKey.Add((byte)i);
                    Children[i]!.CollectAll(output, currentKey);
                    currentKey.RemoveAt(currentKey.Count - 1);
                }
            }
        }
    }

    private ArtNode? _root;
    private int _count;

    public int Count => _count;

    public void Insert(byte[] key, ulong value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        _root = InsertRecursive(_root, key, value, 0);
        _count++;
    }

    private ArtNode InsertRecursive(ArtNode? node, byte[] key, ulong value, int depth)
    {
        if (node is null)
        {
            return new ArtLeaf(key, value);
        }

        if (node is ArtLeaf leaf)
        {
            if (leaf.Key.AsSpan().SequenceEqual(key))
            {
                leaf.Value = value;
                _count--; // Overwrite duplicate
                return leaf;
            }

            var n4 = new Node4();
            int commonPrefix = 0;
            while (depth + commonPrefix < key.Length &&
                   depth + commonPrefix < leaf.Key.Length &&
                   key[depth + commonPrefix] == leaf.Key[depth + commonPrefix])
            {
                commonPrefix++;
            }

            if (commonPrefix > 0)
            {
                n4.Prefix = new byte[commonPrefix];
                Array.Copy(key, depth, n4.Prefix, 0, commonPrefix);
                n4.PrefixLen = commonPrefix;
                depth += commonPrefix;
            }

            byte leafByte = depth < leaf.Key.Length ? leaf.Key[depth] : (byte)0;
            byte keyByte = depth < key.Length ? key[depth] : (byte)0;

            n4.AddOrUpdateChild(leafByte, leaf);
            n4.AddOrUpdateChild(keyByte, new ArtLeaf(key, value));
            return n4;
        }

        // Check common prefix
        int prefixMatch = 0;
        while (prefixMatch < node.PrefixLen &&
               depth + prefixMatch < key.Length &&
               node.Prefix[prefixMatch] == key[depth + prefixMatch])
        {
            prefixMatch++;
        }

        if (prefixMatch < node.PrefixLen)
        {
            var splitNode = new Node4();
            splitNode.Prefix = new byte[prefixMatch];
            Array.Copy(node.Prefix, 0, splitNode.Prefix, 0, prefixMatch);
            splitNode.PrefixLen = prefixMatch;

            byte oldNodeByte = node.Prefix[prefixMatch];
            var truncatedPrefix = new byte[node.PrefixLen - prefixMatch - 1];
            Array.Copy(node.Prefix, prefixMatch + 1, truncatedPrefix, 0, truncatedPrefix.Length);
            node.Prefix = truncatedPrefix;
            node.PrefixLen = truncatedPrefix.Length;

            splitNode.AddOrUpdateChild(oldNodeByte, node);
            byte newByte = (depth + prefixMatch < key.Length) ? key[depth + prefixMatch] : (byte)0;
            splitNode.AddOrUpdateChild(newByte, new ArtLeaf(key, value));
            return splitNode;
        }

        depth += node.PrefixLen;
        byte nextByte = depth < key.Length ? key[depth] : (byte)0;

        if (node.TryFindChild(nextByte, out var child))
        {
            var updated = InsertRecursive(child, key, value, depth + 1);
            return node.AddOrUpdateChild(nextByte, updated);
        }
        else
        {
            return node.AddOrUpdateChild(nextByte, new ArtLeaf(key, value));
        }
    }

    public bool TrySearch(byte[] key, out ulong value)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        value = 0;

        var current = _root;
        int depth = 0;

        while (current is not null)
        {
            if (current is ArtLeaf leaf)
            {
                if (leaf.Key.AsSpan().SequenceEqual(key))
                {
                    value = leaf.Value;
                    return true;
                }
                return false;
            }

            for (int i = 0; i < current.PrefixLen; i++)
            {
                if (depth + i >= key.Length || key[depth + i] != current.Prefix[i])
                    return false;
            }
            depth += current.PrefixLen;

            byte nextByte = depth < key.Length ? key[depth] : (byte)0;
            if (!current.TryFindChild(nextByte, out current))
                return false;

            depth++;
        }

        return false;
    }

    public List<(byte[] Key, ulong Value)> PrefixSearch(byte[] prefix)
    {
        var results = new List<(byte[] Key, ulong Value)>();
        var current = _root;
        int depth = 0;

        while (current is not null && depth < prefix.Length)
        {
            if (current is ArtLeaf leaf)
            {
                if (leaf.Key.AsSpan().StartsWith(prefix))
                {
                    results.Add((leaf.Key, leaf.Value));
                }
                return results;
            }

            int matchLen = Math.Min(current.PrefixLen, prefix.Length - depth);
            for (int i = 0; i < matchLen; i++)
            {
                if (current.Prefix[i] != prefix[depth + i])
                    return results;
            }

            depth += current.PrefixLen;
            if (depth >= prefix.Length)
                break;

            byte nextByte = prefix[depth];
            if (!current.TryFindChild(nextByte, out current))
                return results;

            depth++;
        }

        if (current is not null)
        {
            var path = new List<byte>();
            current.CollectAll(results, path);
        }

        return results;
    }
}
