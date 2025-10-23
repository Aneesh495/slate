// Transactions/MvccEngine.prg
// MVCC Snapshot Isolation and conflict resolution predicates in native X#

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.Core.Transactions

    /// <summary>
    /// Multi-Version Concurrency Control evaluation logic implemented in X#.
    /// </summary>
    PUBLIC CLASS MvccConflictResolver
        PUBLIC STATIC METHOD CanCommitWithoutConflict(writeTimestamp AS UINT64, readSnapshotTimestamp AS UINT64) AS LOGIC
            // If the key was updated after our read snapshot, a write-write conflict has occurred
            IF writeTimestamp > readSnapshotTimestamp
                RETURN FALSE
            ENDIF
            RETURN TRUE

        PUBLIC STATIC METHOD IsRecordVisibleAtSnapshot(createdTs AS UINT64, expiredTs AS UINT64, snapshotTs AS UINT64) AS LOGIC
            // Record is visible if created on or before snapshot, and expired after snapshot (or not expired: 0)
            IF createdTs > snapshotTs
                RETURN FALSE
            ENDIF

            IF expiredTs != 0 .AND. expiredTs <= snapshotTs
                RETURN FALSE
            ENDIF

            RETURN TRUE
    END CLASS

END NAMESPACE
