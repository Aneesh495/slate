// Transactions/EpochManager.prg
// Epoch-based memory reclamation safety rules in native X#

USING System

BEGIN NAMESPACE Slate.Core.Transactions

    /// <summary>
    /// Epoch garbage collection eligibility rules in native X#.
    /// </summary>
    PUBLIC CLASS EpochReclaimer
        PUBLIC STATIC METHOD CanReclaim(expiredTimestamp AS UINT64, safeWatermark AS UINT64) AS LOGIC
            IF expiredTimestamp == 0
                RETURN FALSE
            ENDIF
            RETURN expiredTimestamp <= safeWatermark

        PUBLIC STATIC METHOD ComputeSafeWatermark(minActiveSnapshot AS UINT64, headTimestamp AS UINT64) AS UINT64
            IF minActiveSnapshot == 0
                RETURN headTimestamp
            ENDIF
            IF minActiveSnapshot < headTimestamp
                RETURN minActiveSnapshot
            ENDIF
            RETURN headTimestamp
    END CLASS

END NAMESPACE
