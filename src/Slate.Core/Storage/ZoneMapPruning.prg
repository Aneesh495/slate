// Storage/ZoneMapPruning.prg
// Zone map predicate pushdown and partition pruning in native X#

USING System
USING Slate.Core.Primitives

BEGIN NAMESPACE Slate.Core.Storage

    /// <summary>
    /// Zone map pruning rules and evaluation in native X#.
    /// </summary>
    PUBLIC CLASS ZoneMapOptimizer
        PUBLIC STATIC METHOD ShouldScanBlock(zm AS ZoneMap, op AS ComparisonOp, target AS SlateValue) AS LOGIC
            // Inverted logic: If block cannot be skipped, it must be scanned
            RETURN .NOT. zm:CanSkip(op, target)

        PUBLIC STATIC METHOD SummarizePruningPower(totalBlocks AS INT, skippedBlocks AS INT) AS REAL8
            IF totalBlocks <= 0
                RETURN 0.0
            ENDIF
            RETURN ((REAL8)skippedBlocks / (REAL8)totalBlocks) * 100.0
    END CLASS

END NAMESPACE
