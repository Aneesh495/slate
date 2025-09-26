// Storage/WalJournal.prg
// Write-Ahead Log journal diagnostics and replay ledger in native X#

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.Core.Storage

    /// <summary>
    /// Transaction log analysis and recovery boundary verification in native X#.
    /// </summary>
    PUBLIC CLASS WalJournalAnalyzer
        PUBLIC STATIC METHOD CountCommittedTransactions(records AS List<WalRecord>) AS INT
            IF records == NULL .OR. records:Count == 0
                RETURN 0
            ENDIF

            LOCAL committedTxs := HashSet<UINT64>{} AS HashSet<UINT64>
            LOCAL i AS INT
            FOR i := 0 UPTO records:Count - 1
                LOCAL rec := records[i] AS WalRecord
                IF rec:RecordType == WalRecordType.Commit
                    committedTxs:Add(rec:TxId)
                ENDIF
            NEXT

            RETURN committedTxs:Count

        PUBLIC STATIC METHOD FindHighestLsn(records AS List<WalRecord>) AS UINT64
            IF records == NULL .OR. records:Count == 0
                RETURN 0
            ENDIF

            LOCAL maxLsn := 0 AS UINT64
            LOCAL i AS INT
            FOR i := 0 UPTO records:Count - 1
                LOCAL rec := records[i] AS WalRecord
                IF rec:Lsn > maxLsn
                    maxLsn := rec:Lsn
                ENDIF
            NEXT

            RETURN maxLsn
    END CLASS

END NAMESPACE
