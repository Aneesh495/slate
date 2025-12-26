using System.Collections.Concurrent;
using Slate.Core.Primitives;

namespace Slate.Core.Storage;

public sealed record MemTableEntry(
    SlateValue Key,
    SlateValue[] Values,
    ulong TxId,
    ulong CommitTimestamp,
    bool IsDeleted);

/// <summary>
/// A concurrent, sorted in-memory MemTable buffer holding active transactional writes.
/// Converts in-memory row mutations into vectorized ColumnVector batches during SSTable flushes.
/// </summary>
public sealed class MemTable
{
    private readonly ConcurrentDictionary<SlateValue, LinkedList<MemTableEntry>> _entries = new();
    private readonly ReaderWriterLockSlim _rwLock = new(LockRecursionPolicy.SupportsRecursion);
    private long _approximateBytes;

    public int RowCount => _entries.Count;
    public long ApproximateBytes => Interlocked.Read(ref _approximateBytes);

    public void Put(SlateValue key, SlateValue[] values, ulong txId, ulong commitTimestamp)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var entry = new MemTableEntry(key, values, txId, commitTimestamp, IsDeleted: false);
            var list = _entries.GetOrAdd(key, _ => new LinkedList<MemTableEntry>());
            list.AddFirst(entry);

            // Estimate memory overhead: 32 bytes entry overhead + values
            long addedBytes = 32 + (values.Length * 16);
            Interlocked.Add(ref _approximateBytes, addedBytes);
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    public void Delete(SlateValue key, ulong txId, ulong commitTimestamp)
    {
        _rwLock.EnterWriteLock();
        try
        {
            var entry = new MemTableEntry(key, Array.Empty<SlateValue>(), txId, commitTimestamp, IsDeleted: true);
            var list = _entries.GetOrAdd(key, _ => new LinkedList<MemTableEntry>());
            list.AddFirst(entry);
            Interlocked.Add(ref _approximateBytes, 32);
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    public void Commit(SlateValue key, ulong txId, ulong commitTimestamp)
    {
        _rwLock.EnterWriteLock();
        try
        {
            if (_entries.TryGetValue(key, out var list))
            {
                var current = list.First;
                while (current != null)
                {
                    if (current.Value.TxId == txId && current.Value.CommitTimestamp == ulong.MaxValue)
                    {
                        current.Value = current.Value with { CommitTimestamp = commitTimestamp };
                    }
                    current = current.Next;
                }
            }
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    public void Rollback(SlateValue key, ulong txId)
    {
        _rwLock.EnterWriteLock();
        try
        {
            if (_entries.TryGetValue(key, out var list))
            {
                var current = list.First;
                while (current != null)
                {
                    var next = current.Next;
                    if (current.Value.TxId == txId && current.Value.CommitTimestamp == ulong.MaxValue)
                    {
                        list.Remove(current);
                    }
                    current = next;
                }
            }
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    public MemTableEntry? Get(SlateValue key, ulong readTimestamp)
    {
        _rwLock.EnterReadLock();
        try
        {
            return GetNoLock(key, readTimestamp);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    private MemTableEntry? GetNoLock(SlateValue key, ulong readTimestamp)
    {
        if (!_entries.TryGetValue(key, out var list))
            return null;

        foreach (var entry in list)
        {
            if (entry.CommitTimestamp <= readTimestamp)
            {
                return entry.IsDeleted ? null : entry;
            }
        }

        return null;
    }

    public List<MemTableEntry> Scan(SlateValue? startKey, SlateValue? endKey, ulong readTimestamp)
    {
        _rwLock.EnterReadLock();
        try
        {
            var results = new List<MemTableEntry>();
            var sortedKeys = _entries.Keys.OrderBy(k => k).ToList();

            foreach (var key in sortedKeys)
            {
                if (startKey.HasValue && key.CompareTo(startKey.Value) < 0)
                    continue;
                if (endKey.HasValue && key.CompareTo(endKey.Value) > 0)
                    break;

                var entry = GetNoLock(key, readTimestamp);
                if (entry is not null)
                {
                    results.Add(entry);
                }
            }

            return results;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    /// <summary>
    /// Freezes and transforms active MemTable rows into a columnar VectorBatch for SSTable serialization.
    /// </summary>
    public VectorBatch FlushToVectorBatch(TableSchema schema, ulong snapshotTimestamp)
    {
        _rwLock.EnterReadLock();
        try
        {
            var activeEntries = new List<MemTableEntry>();
            var sortedKeys = _entries.Keys.OrderBy(k => k).ToList();

            foreach (var key in sortedKeys)
            {
                var entry = GetNoLock(key, snapshotTimestamp);
                if (entry is not null)
                {
                    activeEntries.Add(entry);
                }
            }

            int count = activeEntries.Count;
            var columns = new ColumnVector[schema.ColumnCount];
            for (int c = 0; c < schema.ColumnCount; c++)
            {
                columns[c] = new ColumnVector(schema[c].Type, Math.Max(1, count));
            }

            for (int r = 0; r < count; r++)
            {
                var entry = activeEntries[r];
                for (int c = 0; c < schema.ColumnCount; c++)
                {
                    var val = c < entry.Values.Length ? entry.Values[c] : SlateValue.Null;
                    columns[c].AppendValue(val);
                }
            }

            return new VectorBatch(schema, columns, count);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public void Clear()
    {
        _rwLock.EnterWriteLock();
        try
        {
            _entries.Clear();
            Interlocked.Exchange(ref _approximateBytes, 0);
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }
}
