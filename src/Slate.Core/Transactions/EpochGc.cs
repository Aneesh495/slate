using System.Collections.Concurrent;
using Slate.Core.Primitives;

namespace Slate.Core.Transactions;

public sealed record RetiredEntry(TableKey Key, ulong ExpiredTimestamp, Action? ReclaimAction);

/// <summary>
/// Epoch-based garbage collector for safely reclaiming obsolete MVCC versions and deleted index entries.
/// An entry is eligible for physical purge only when its expiration timestamp is strictly less than
/// or equal to the minimum read snapshot timestamp across all currently active transactions.
/// </summary>
public sealed class EpochGc
{
    private readonly ConcurrentBag<RetiredEntry> _retiredEntries = new();
    private readonly object _gcLock = new();

    public int PendingRetirementsCount => _retiredEntries.Count;

    public void Retire(TableKey key, ulong expiredTimestamp, Action? reclaimAction = null)
    {
        _retiredEntries.Add(new RetiredEntry(key, expiredTimestamp, reclaimAction));
    }

    public static ulong ComputeSafeWatermark(IReadOnlyCollection<TransactionContext> activeTransactions, ulong currentTimestamp)
    {
        if (activeTransactions == null || activeTransactions.Count == 0)
            return currentTimestamp;

        ulong minTs = currentTimestamp;
        foreach (var tx in activeTransactions)
        {
            if (tx.Status == TransactionStatus.Active && tx.ReadTimestamp < minTs)
            {
                minTs = tx.ReadTimestamp;
            }
        }
        return minTs;
    }

    public int Collect(ulong safeWatermark)
    {
        lock (_gcLock)
        {
            var remaining = new List<RetiredEntry>();
            int reclaimedCount = 0;

            while (_retiredEntries.TryTake(out var entry))
            {
                if (entry.ExpiredTimestamp <= safeWatermark)
                {
                    try
                    {
                        entry.ReclaimAction?.Invoke();
                    }
                    catch
                    {
                        // Suppress individual reclamation callback errors to prevent GC thread disruption
                    }
                    reclaimedCount++;
                }
                else
                {
                    remaining.Add(entry);
                }
            }

            foreach (var rem in remaining)
            {
                _retiredEntries.Add(rem);
            }

            return reclaimedCount;
        }
    }
}
