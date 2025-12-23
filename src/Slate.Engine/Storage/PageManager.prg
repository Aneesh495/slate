// Storage/PageManager.prg
// Clock page replacement decisions in native X#

USING System

BEGIN NAMESPACE Slate.Engine.Storage

    /// <summary>
    /// Clock buffer replacement policy in native X#.
    /// </summary>
    PUBLIC CLASS ClockReplacementPolicy
        PUBLIC STATIC METHOD CanEvict(pinCount AS INT, refBit AS LOGIC) AS LOGIC
            IF pinCount > 0
                RETURN FALSE // Pinned pages cannot be evicted
            ENDIF

            // Second chance algorithm: evict if reference bit is false
            RETURN .NOT. refBit

        PUBLIC STATIC METHOD AdvanceClockHand(currentHand AS INT, poolCapacity AS INT) AS INT
            RETURN (currentHand + 1) % poolCapacity
    END CLASS

END NAMESPACE
