using Slate.Core.Primitives;
using Slate.Core.Storage;

namespace Slate.Core.Transactions;

public sealed class RecoveryResult
{
    public HashSet<ulong> WinnerTransactions { get; } = new();
    public HashSet<ulong> LoserTransactions { get; } = new();
    public int RedoneRecordsCount { get; set; }
    public int UndoneRecordsCount { get; set; }
    public ulong MaxRecoveredLsn { get; set; }
}

/// <summary>
/// ARIES-style crash recovery engine implementing Analysis, Redo, and Undo phases.
/// Repeats history during Redo, then rolls back uncommitted loser transactions during Undo.
/// </summary>
public sealed class AriesRecoveryEngine
{
    public static RecoveryResult Recover(Stream walStream, MemTable targetMemTable, MvccManager targetMvcc)
    {
        var result = new RecoveryResult();
        var records = WalReader.ReadAll(walStream, out _);

        if (records.Count == 0)
            return result;

        // Phase 1: Analysis
        // Scan forward to determine transaction status at crash time
        var txStatus = new Dictionary<ulong, TransactionStatus>();

        foreach (var rec in records)
        {
            if (rec.Lsn > result.MaxRecoveredLsn)
                result.MaxRecoveredLsn = rec.Lsn;

            switch (rec.RecordType)
            {
                case WalRecordType.Begin:
                    txStatus[rec.TxId] = TransactionStatus.Active;
                    break;
                case WalRecordType.Commit:
                    txStatus[rec.TxId] = TransactionStatus.Committed;
                    break;
                case WalRecordType.Abort:
                    txStatus[rec.TxId] = TransactionStatus.Aborted;
                    break;
                case WalRecordType.Insert:
                case WalRecordType.Update:
                case WalRecordType.Delete:
                    if (!txStatus.ContainsKey(rec.TxId))
                    {
                        txStatus[rec.TxId] = TransactionStatus.Active;
                    }
                    break;
            }
        }

        foreach (var kvp in txStatus)
        {
            if (kvp.Value == TransactionStatus.Committed)
            {
                result.WinnerTransactions.Add(kvp.Key);
            }
            else
            {
                result.LoserTransactions.Add(kvp.Key);
            }
        }

        // Phase 2: Redo (Repeating History)
        // Replay all operations in log order
        foreach (var rec in records)
        {
            if (rec.RecordType == WalRecordType.Insert || rec.RecordType == WalRecordType.Update)
            {
                targetMemTable.Put(rec.PrimaryKey, rec.Values, rec.TxId, rec.Lsn);
                result.RedoneRecordsCount++;
            }
            else if (rec.RecordType == WalRecordType.Delete)
            {
                targetMemTable.Delete(rec.PrimaryKey, rec.TxId, rec.Lsn);
                result.RedoneRecordsCount++;
            }
        }

        // Phase 3: Undo
        // Roll back uncommitted changes made by loser transactions
        // Scan backward through log to undo loser actions
        for (int i = records.Count - 1; i >= 0; i--)
        {
            var rec = records[i];
            if (result.LoserTransactions.Contains(rec.TxId))
            {
                if (rec.RecordType == WalRecordType.Insert || rec.RecordType == WalRecordType.Update)
                {
                    targetMemTable.Delete(rec.PrimaryKey, rec.TxId, result.MaxRecoveredLsn + 1);
                    result.UndoneRecordsCount++;
                }
            }
        }

        return result;
    }
}
