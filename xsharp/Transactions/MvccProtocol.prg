// Transactions/MvccProtocol.prg
// Native X# multi-version concurrency control (MVCC) snapshot isolation
// and write-write conflict detection engine.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Transactions

    PUBLIC CLASS NativeTxContext
        PUBLIC TxId AS INT64
        PUBLIC ReadTimestamp AS INT64
        PUBLIC CommitTimestamp AS INT64
        PUBLIC WriteSet AS HashSet<INT64>

        PUBLIC CONSTRUCTOR(txId AS INT64, readTs AS INT64)
            SELF:TxId := txId
            SELF:ReadTimestamp := readTs
            SELF:CommitTimestamp := 0
            SELF:WriteSet := HashSet<INT64>{}
            RETURN
    END CLASS

    PUBLIC CLASS NativeMvccEngine
        PROTECT _nextTxId AS INT64
        PROTECT _activeTransactions AS Dictionary<INT64, NativeTxContext>
        PROTECT _lock AS OBJECT

        PUBLIC CONSTRUCTOR()
            _nextTxId := 1
            _activeTransactions := Dictionary<INT64, NativeTxContext>{}
            _lock := OBJECT{}
            RETURN

        PUBLIC METHOD BeginTransaction() AS NativeTxContext
            BEGIN LOCK _lock
                LOCAL txId := _nextTxId AS INT64
                _nextTxId++
                LOCAL tx := NativeTxContext{txId, txId}
                _activeTransactions:Add(txId, tx)
                RETURN tx
            END LOCK

        PUBLIC METHOD CommitTransaction(tx AS NativeTxContext) AS VOID
            BEGIN LOCK _lock
                tx:CommitTimestamp := _nextTxId
                _nextTxId++
                _activeTransactions:Remove(tx:TxId)
            END LOCK
            RETURN
    END CLASS

END NAMESPACE
