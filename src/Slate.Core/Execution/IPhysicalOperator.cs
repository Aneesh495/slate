using System.Diagnostics;
using Slate.Core.Primitives;
using Slate.Core.Transactions;

namespace Slate.Core.Execution;

public sealed class QueryExecutionContext
{
    public TransactionContext? Transaction { get; }
    public ulong ReadTimestamp { get; }
    public long MemoryBudgetBytes { get; }
    public CancellationToken CancellationToken { get; }

    public QueryExecutionContext(
        TransactionContext? transaction = null,
        ulong readTimestamp = ulong.MaxValue,
        long memoryBudgetBytes = 1024 * 1024 * 1024, // 1 GB default
        CancellationToken cancellationToken = default)
    {
        Transaction = transaction;
        ReadTimestamp = transaction?.ReadTimestamp ?? readTimestamp;
        MemoryBudgetBytes = memoryBudgetBytes;
        CancellationToken = cancellationToken;
    }
}

/// <summary>
/// Volcano-style vectorized iterator interface executing queries in columnar VectorBatch chunks.
/// </summary>
public interface IPhysicalOperator : IDisposable
{
    TableSchema Schema { get; }
    IReadOnlyList<IPhysicalOperator> Children { get; }
    bool IsOpen { get; }
    long RowsProduced { get; }
    long ExecutionTimeNs { get; }

    void Open(QueryExecutionContext context);
    VectorBatch? Next();
    void Close();
}

public abstract class PhysicalOperatorBase : IPhysicalOperator
{
    private readonly Stopwatch _stopwatch = new();
    private bool _isOpen;
    private long _rowsProduced;

    public abstract TableSchema Schema { get; }
    public abstract IReadOnlyList<IPhysicalOperator> Children { get; }

    public bool IsOpen => _isOpen;
    public long RowsProduced => _rowsProduced;
    public long ExecutionTimeNs => (long)(_stopwatch.Elapsed.TotalMilliseconds * 1_000_000.0);

    protected QueryExecutionContext Context { get; private set; } = null!;

    public void Open(QueryExecutionContext context)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        foreach (var child in Children)
        {
            child.Open(context);
        }
        OnOpen();
        _isOpen = true;
    }

    public VectorBatch? Next()
    {
        if (!_isOpen)
            throw new InvalidOperationException("Operator must be opened before calling Next().");

        Context.CancellationToken.ThrowIfCancellationRequested();

        _stopwatch.Start();
        try
        {
            var batch = OnNext();
            if (batch != null)
            {
                _rowsProduced += batch.RowCount;
            }
            return batch;
        }
        finally
        {
            _stopwatch.Stop();
        }
    }

    public void Close()
    {
        if (!_isOpen) return;

        OnClose();
        foreach (var child in Children)
        {
            child.Close();
        }
        _isOpen = false;
    }

    public void Dispose()
    {
        Close();
        foreach (var child in Children)
        {
            child.Dispose();
        }
        GC.SuppressFinalize(this);
    }

    protected abstract void OnOpen();
    protected abstract VectorBatch? OnNext();
    protected abstract void OnClose();
}
