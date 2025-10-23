using System.Collections.Concurrent;
using Slate.Core.Primitives;
using Slate.Core.Storage;

namespace Slate.Core.Transactions;

public sealed class WriteConflictException : Exception
{
    public WriteConflictException(string message) : base(message) { }
}

public static class MvccConflictResolver
{
    public static bool CanCommitWithoutConflict(ulong writeTimestamp, ulong readSnapshotTimestamp)
    {
        return writeTimestamp <= readSnapshotTimestamp;
    }

    public static bool IsRecordVisibleAtSnapshot(ulong createdTs, ulong expiredTs, ulong snapshotTs)
    {
        if (createdTs > snapshotTs)
            return false;
        if (expiredTs != 0 && expiredTs <= snapshotTs)
            return false;
        return true;
    }
}

public sealed class VersionRecord
{
    public ulong CreatedTxId { get; set; }
    public ulong CreatedTimestamp { get; set; }
    public ulong ExpiredTxId { get; set; }
    public ulong ExpiredTimestamp { get; set; }
    public SlateValue Value { get; set; }
    public bool IsDeleted { get; set; }

    public VersionRecord(ulong createdTxId, ulong createdTimestamp, ulong expiredTxId, ulong expiredTimestamp, SlateValue value, bool isDeleted)
    {
        CreatedTxId = createdTxId;
        CreatedTimestamp = createdTimestamp;
        ExpiredTxId = expiredTxId;
        ExpiredTimestamp = expiredTimestamp;
        Value = value;
        IsDeleted = isDeleted;
    }
}

/// <summary>
/// Multi-Version Concurrency Control (MVCC) manager providing Snapshot Isolation
/// and Serializable isolation levels with write-write conflict detection.
/// </summary>
public sealed class MvccManager
{
    private long _currentTimestamp;
    private long _currentTxId;
    private readonly ConcurrentDictionary<ulong, TransactionContext> _activeTransactions = new();
    private readonly ConcurrentDictionary<TableKey, ulong> _latestCommittedKeyTimestamps = new();
    private readonly ConcurrentDictionary<TableKey, List<VersionRecord>> _versionStore = new();
    private readonly object _commitLock = new();

    public ulong CurrentTimestamp => (ulong)Interlocked.Read(ref _currentTimestamp);

    public MvccManager(ulong initialTimestamp = 1000)
    {
        _currentTimestamp = (long)initialTimestamp;
        _currentTxId = (long)initialTimestamp;
    }

    public TransactionContext BeginTransaction(IsolationLevel isolation = IsolationLevel.SnapshotIsolation)
    {
        ulong txId = (ulong)Interlocked.Increment(ref _currentTxId);
        ulong readTimestamp = (ulong)Interlocked.Read(ref _currentTimestamp);

        var tx = new TransactionContext(txId, readTimestamp, isolation);
        _activeTransactions[txId] = tx;
        return tx;
    }

    public void CommitTransaction(TransactionContext tx, WalWriter? walWriter = null)
    {
        if (tx.Status != TransactionStatus.Active)
            throw new InvalidOperationException($"Cannot commit transaction {tx.TxId} in state {tx.Status}.");

        lock (_commitLock)
        {
            // 1. Validate write-write conflicts: First-committer-wins
            foreach (var key in tx.WriteSet)
            {
                if (_latestCommittedKeyTimestamps.TryGetValue(key, out ulong latestCommitTs))
                {
                    if (latestCommitTs > tx.ReadTimestamp)
                    {
                        tx.MarkAborted();
                        _activeTransactions.TryRemove(tx.TxId, out _);
                        throw new WriteConflictException($"Write-write conflict on key '{key.Key}' in table '{key.TableName}'. Key committed at {latestCommitTs} after tx read timestamp {tx.ReadTimestamp}.");
                    }
                }
            }

            // 2. Validate read-write conflicts for Serializable isolation
            if (tx.Isolation == IsolationLevel.Serializable)
            {
                foreach (var readKey in tx.ReadSet)
                {
                    if (_latestCommittedKeyTimestamps.TryGetValue(readKey, out ulong modifiedTs))
                    {
                        if (modifiedTs > tx.ReadTimestamp)
                        {
                            tx.MarkAborted();
                            _activeTransactions.TryRemove(tx.TxId, out _);
                            throw new WriteConflictException($"Serialization anomaly detected on read key '{readKey.Key}' in table '{readKey.TableName}'.");
                        }
                    }
                }
            }

            // 3. Assign monotonic commit timestamp
            ulong commitTs = (ulong)Interlocked.Increment(ref _currentTimestamp);

            // 4. Update latest committed timestamps and version store records
            foreach (var key in tx.WriteSet)
            {
                _latestCommittedKeyTimestamps[key] = commitTs;
                if (_versionStore.TryGetValue(key, out var list))
                {
                    lock (list)
                    {
                        foreach (var v in list)
                        {
                            if (v.CreatedTxId == tx.TxId && v.CreatedTimestamp == 0)
                            {
                                v.CreatedTimestamp = commitTs;
                            }
                        }
                    }
                }
            }

            // 5. Append commit record to WAL if attached
            if (walWriter != null)
            {
                walWriter.Append(tx.TxId, WalRecordType.Commit, string.Empty, SlateValue.Null, Array.Empty<SlateValue>());
            }

            // 6. Transition transaction status
            tx.MarkCommitted(commitTs);
            _activeTransactions.TryRemove(tx.TxId, out _);
        }
    }

    public void AbortTransaction(TransactionContext tx, WalWriter? walWriter = null)
    {
        if (tx.Status != TransactionStatus.Active)
            return;

        lock (_commitLock)
        {
            tx.MarkAborted();
            _activeTransactions.TryRemove(tx.TxId, out _);

            foreach (var key in tx.WriteSet)
            {
                if (_versionStore.TryGetValue(key, out var list))
                {
                    lock (list)
                    {
                        list.RemoveAll(v => v.CreatedTxId == tx.TxId && v.CreatedTimestamp == 0);
                    }
                }
            }

            if (walWriter != null)
            {
                walWriter.Append(tx.TxId, WalRecordType.Abort, string.Empty, SlateValue.Null, Array.Empty<SlateValue>());
            }
        }
    }

    public void RecordVersion(string tableName, SlateValue key, SlateValue value, TransactionContext tx, bool isDeleted = false)
    {
        var tableKey = new TableKey(tableName, key);
        tx.RecordWrite(tableName, key);

        var version = new VersionRecord(
            createdTxId: tx.TxId,
            createdTimestamp: 0,
            expiredTxId: 0,
            expiredTimestamp: 0,
            value: value,
            isDeleted: isDeleted
        );

        _versionStore.AddOrUpdate(tableKey,
            _ => new List<VersionRecord> { version },
            (_, list) =>
            {
                lock (list)
                {
                    list.Add(version);
                }
                return list;
            });
    }

    public SlateValue? ReadVersion(string tableName, SlateValue key, TransactionContext tx)
    {
        var tableKey = new TableKey(tableName, key);
        tx.RecordRead(tableName, key);

        if (!_versionStore.TryGetValue(tableKey, out var list))
            return null;

        lock (list)
        {
            // Find newest version visible to this transaction
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var v = list[i];
                // Visible if created by our own transaction or created and committed before read timestamp
                bool isVisible = (v.CreatedTxId == tx.TxId) || (v.CreatedTimestamp != 0 && v.CreatedTimestamp <= tx.ReadTimestamp);
                if (isVisible)
                {
                    return v.IsDeleted ? null : v.Value;
                }
            }
        }

        return null;
    }

    public IReadOnlyCollection<TransactionContext> GetActiveTransactions()
    {
        return _activeTransactions.Values.ToList();
    }
}
