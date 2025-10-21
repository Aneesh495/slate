// Transactions/TxContext.prg
// Transaction timestamp generation and status checking in native X#

USING System

BEGIN NAMESPACE Slate.Core.Transactions

    /// <summary>
    /// Transaction timestamp sequencing and lifecycle analysis in native X#.
    /// </summary>
    PUBLIC CLASS TransactionSequencer
        PUBLIC STATIC METHOD IsVisible(txCommitTimestamp AS UINT64, readSnapshotTimestamp AS UINT64) AS LOGIC
            // In snapshot isolation, a row is visible if its commit timestamp <= read snapshot timestamp
            IF txCommitTimestamp == 0
                RETURN FALSE
            ENDIF
            RETURN txCommitTimestamp <= readSnapshotTimestamp

        PUBLIC STATIC METHOD HasConflictingWrite(writeSetCount AS INT, concurrentCommits AS INT) AS LOGIC
            RETURN writeSetCount > 0 .AND. concurrentCommits > 0
    END CLASS

END NAMESPACE
