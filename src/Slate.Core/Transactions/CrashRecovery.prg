// Transactions/CrashRecovery.prg
// ARIES crash recovery phase classification in native X#

USING System

BEGIN NAMESPACE Slate.Core.Transactions

    /// <summary>
    /// ARIES recovery classification and redo decisions in native X#.
    /// </summary>
    PUBLIC CLASS AriesClassifier
        PUBLIC STATIC METHOD IsLoserTransaction(isCommitted AS LOGIC, isAborted AS LOGIC) AS LOGIC
            IF isCommitted
                RETURN FALSE
            ENDIF
            RETURN TRUE

        PUBLIC STATIC METHOD NeedsRedo(recordLsn AS UINT64, pageLsn AS UINT64) AS LOGIC
            // ARIES rule: Redo is needed if the log record's LSN is strictly greater than page LSN
            RETURN recordLsn > pageLsn
    END CLASS

END NAMESPACE
