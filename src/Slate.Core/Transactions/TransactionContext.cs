using Slate.Core.Primitives;

namespace Slate.Core.Transactions;

public enum TransactionStatus : byte
{
    Active = 1,
    Committed = 2,
    Aborted = 3
}

public enum IsolationLevel : byte
{
    ReadCommitted = 1,
    SnapshotIsolation = 2,
    Serializable = 3
}

public sealed record TableKey(string TableName, SlateValue Key);

/// <summary>
/// Execution state, read snapshot timestamps, and write-set trackers for an active ACID transaction.
/// </summary>
public sealed class TransactionContext
{
    public ulong TxId { get; }
    public ulong ReadTimestamp { get; }
    public ulong CommitTimestamp { get; private set; }
    public TransactionStatus Status { get; private set; }
    public IsolationLevel Isolation { get; }

    private readonly HashSet<TableKey> _writeSet = new();
    private readonly HashSet<TableKey> _readSet = new();
    private readonly object _lock = new();

    public IReadOnlySet<TableKey> WriteSet => _writeSet;
    public IReadOnlySet<TableKey> ReadSet => _readSet;

    public TransactionContext(ulong txId, ulong readTimestamp, IsolationLevel isolation = IsolationLevel.SnapshotIsolation)
    {
        TxId = txId;
        ReadTimestamp = readTimestamp;
        CommitTimestamp = 0;
        Status = TransactionStatus.Active;
        Isolation = isolation;
    }

    public void RecordWrite(string tableName, SlateValue key)
    {
        lock (_lock)
        {
            if (Status != TransactionStatus.Active)
                throw new InvalidOperationException($"Cannot record writes on non-active transaction {TxId} (Status={Status}).");

            _writeSet.Add(new TableKey(tableName, key));
        }
    }

    public void RecordRead(string tableName, SlateValue key)
    {
        lock (_lock)
        {
            if (Isolation == IsolationLevel.Serializable && Status == TransactionStatus.Active)
            {
                _readSet.Add(new TableKey(tableName, key));
            }
        }
    }

    public void MarkCommitted(ulong commitTimestamp)
    {
        lock (_lock)
        {
            if (Status != TransactionStatus.Active)
                throw new InvalidOperationException($"Cannot commit non-active transaction {TxId}.");

            CommitTimestamp = commitTimestamp;
            Status = TransactionStatus.Committed;
        }
    }

    public void MarkAborted()
    {
        lock (_lock)
        {
            Status = TransactionStatus.Aborted;
        }
    }
}
