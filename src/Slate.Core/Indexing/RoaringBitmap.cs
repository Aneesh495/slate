using System.Numerics;

namespace Slate.Core.Indexing;

/// <summary>
/// A high-performance 64-bit Roaring Bitmap implementation with dynamic container switching
/// between ArrayContainer (sparse) and BitsetContainer (dense).
/// Used for inverted indexes, multi-attribute filter evaluations, and candidate row set intersections.
/// </summary>
public sealed class RoaringBitmap
{
    private interface IContainer
    {
        int Cardinality { get; }
        bool Contains(ushort val);
        IContainer Add(ushort val);
        IContainer Remove(ushort val);
        IContainer And(IContainer other);
        IContainer Or(IContainer other);
        void Enumerate(ulong highPart, List<ulong> output);
    }

    private sealed class ArrayContainer : IContainer
    {
        private ushort[] _values;
        private int _count;

        public int Cardinality => _count;

        public ArrayContainer()
        {
            _values = new ushort[8];
            _count = 0;
        }

        private ArrayContainer(ushort[] values, int count)
        {
            _values = values;
            _count = count;
        }

        public bool Contains(ushort val) => Array.BinarySearch(_values, 0, _count, val) >= 0;

        public IContainer Add(ushort val)
        {
            int idx = Array.BinarySearch(_values, 0, _count, val);
            if (idx >= 0) return this;

            int insertAt = ~idx;
            if (_count == 4096)
            {
                // Convert to BitsetContainer
                var bitset = new BitsetContainer();
                for (int i = 0; i < _count; i++) bitset.Add(_values[i]);
                bitset.Add(val);
                return bitset;
            }

            if (_count == _values.Length)
            {
                Array.Resize(ref _values, _values.Length * 2);
            }

            Array.Copy(_values, insertAt, _values, insertAt + 1, _count - insertAt);
            _values[insertAt] = val;
            _count++;
            return this;
        }

        public IContainer Remove(ushort val)
        {
            int idx = Array.BinarySearch(_values, 0, _count, val);
            if (idx < 0) return this;

            Array.Copy(_values, idx + 1, _values, idx, _count - idx - 1);
            _count--;
            return this;
        }

        public IContainer And(IContainer other)
        {
            if (other is ArrayContainer ac)
            {
                var result = new ushort[Math.Min(_count, ac._count)];
                int rCount = 0;
                int i = 0, j = 0;
                while (i < _count && j < ac._count)
                {
                    if (_values[i] == ac._values[j])
                    {
                        result[rCount++] = _values[i];
                        i++; j++;
                    }
                    else if (_values[i] < ac._values[j]) i++;
                    else j++;
                }
                return new ArrayContainer(result, rCount);
            }
            else
            {
                var result = new ushort[_count];
                int rCount = 0;
                for (int i = 0; i < _count; i++)
                {
                    if (other.Contains(_values[i])) result[rCount++] = _values[i];
                }
                return new ArrayContainer(result, rCount);
            }
        }

        public IContainer Or(IContainer other)
        {
            var res = new ArrayContainer();
            for (int i = 0; i < _count; i++) res.Add(_values[i]);
            if (other is ArrayContainer ac)
            {
                for (int j = 0; j < ac._count; j++) res.Add(ac._values[j]);
                return res;
            }
            else
            {
                return other.Or(this);
            }
        }

        public void Enumerate(ulong highPart, List<ulong> output)
        {
            for (int i = 0; i < _count; i++)
            {
                output.Add(highPart | _values[i]);
            }
        }
    }

    private sealed class BitsetContainer : IContainer
    {
        private readonly ulong[] _words = new ulong[1024]; // 1024 * 64 = 65,536 bits
        private int _cardinality;

        public int Cardinality => _cardinality;

        public bool Contains(ushort val)
        {
            int word = val >> 6;
            int bit = val & 63;
            return (_words[word] & (1UL << bit)) != 0;
        }

        public IContainer Add(ushort val)
        {
            int word = val >> 6;
            int bit = val & 63;
            ulong mask = 1UL << bit;
            if ((_words[word] & mask) == 0)
            {
                _words[word] |= mask;
                _cardinality++;
            }
            return this;
        }

        public IContainer Remove(ushort val)
        {
            int word = val >> 6;
            int bit = val & 63;
            ulong mask = 1UL << bit;
            if ((_words[word] & mask) != 0)
            {
                _words[word] &= ~mask;
                _cardinality--;
            }
            return this;
        }

        public IContainer And(IContainer other)
        {
            if (other is BitsetContainer bc)
            {
                var res = new BitsetContainer();
                int card = 0;
                for (int i = 0; i < 1024; i++)
                {
                    res._words[i] = _words[i] & bc._words[i];
                    card += BitOperations.PopCount(res._words[i]);
                }
                res._cardinality = card;
                return res;
            }
            else
            {
                return other.And(this);
            }
        }

        public IContainer Or(IContainer other)
        {
            if (other is BitsetContainer bc)
            {
                var res = new BitsetContainer();
                int card = 0;
                for (int i = 0; i < 1024; i++)
                {
                    res._words[i] = _words[i] | bc._words[i];
                    card += BitOperations.PopCount(res._words[i]);
                }
                res._cardinality = card;
                return res;
            }
            else
            {
                var res = new BitsetContainer();
                Array.Copy(_words, res._words, 1024);
                res._cardinality = _cardinality;
                if (other is ArrayContainer ac)
                {
                    var list = new List<ulong>();
                    ac.Enumerate(0, list);
                    foreach (var v in list) res.Add((ushort)v);
                }
                return res;
            }
        }

        public void Enumerate(ulong highPart, List<ulong> output)
        {
            for (int w = 0; w < 1024; w++)
            {
                ulong word = _words[w];
                while (word != 0)
                {
                    int bit = BitOperations.TrailingZeroCount(word);
                    output.Add(highPart | (ulong)(uint)((w << 6) | bit));
                    word &= word - 1; // Clear lowest set bit
                }
            }
        }
    }

    private readonly Dictionary<ulong, IContainer> _chunks = new();

    public int Cardinality => _chunks.Values.Sum(c => c.Cardinality);

    public void Add(ulong value)
    {
        ulong high = value & ~0xFFFFUL;
        ushort low = (ushort)(value & 0xFFFFUL);

        if (!_chunks.TryGetValue(high, out var container))
        {
            container = new ArrayContainer();
            _chunks[high] = container;
        }

        _chunks[high] = container.Add(low);
    }

    public void Remove(ulong value)
    {
        ulong high = value & ~0xFFFFUL;
        ushort low = (ushort)(value & 0xFFFFUL);

        if (_chunks.TryGetValue(high, out var container))
        {
            _chunks[high] = container.Remove(low);
            if (_chunks[high].Cardinality == 0)
            {
                _chunks.Remove(high);
            }
        }
    }

    public bool Contains(ulong value)
    {
        ulong high = value & ~0xFFFFUL;
        ushort low = (ushort)(value & 0xFFFFUL);
        return _chunks.TryGetValue(high, out var container) && container.Contains(low);
    }

    public RoaringBitmap And(RoaringBitmap other)
    {
        var result = new RoaringBitmap();
        foreach (var (high, container) in _chunks)
        {
            if (other._chunks.TryGetValue(high, out var otherContainer))
            {
                var intersection = container.And(otherContainer);
                if (intersection.Cardinality > 0)
                {
                    result._chunks[high] = intersection;
                }
            }
        }
        return result;
    }

    public RoaringBitmap Or(RoaringBitmap other)
    {
        var result = new RoaringBitmap();
        var allKeys = _chunks.Keys.Union(other._chunks.Keys);
        foreach (var key in allKeys)
        {
            _chunks.TryGetValue(key, out var c1);
            other._chunks.TryGetValue(key, out var c2);

            if (c1 is not null && c2 is not null)
            {
                result._chunks[key] = c1.Or(c2);
            }
            else if (c1 is not null)
            {
                result._chunks[key] = c1;
            }
            else if (c2 is not null)
            {
                result._chunks[key] = c2;
            }
        }
        return result;
    }

    public ulong[] ToArray()
    {
        var output = new List<ulong>(Cardinality);
        var sortedKeys = _chunks.Keys.OrderBy(k => k);
        foreach (var key in sortedKeys)
        {
            _chunks[key].Enumerate(key, output);
        }
        return output.ToArray();
    }
}
