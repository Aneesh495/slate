// Execution/ScanOperators.prg
// Table scan and index scan predicates in native X#

USING System

BEGIN NAMESPACE Slate.Core.Execution

    /// <summary>
    /// Scan operator pruning and zone map evaluation in native X#.
    /// </summary>
    PUBLIC CLASS ScanPruner
        PUBLIC STATIC METHOD CanPruneRange(minVal AS INT64, maxVal AS INT64, queryStart AS INT64, queryEnd AS INT64) AS LOGIC
            // If the query interval is completely outside [minVal, maxVal], prune block
            IF queryEnd < minVal .OR. queryStart > maxVal
                RETURN TRUE
            ENDIF
            RETURN FALSE

        PUBLIC STATIC METHOD ShouldUseIndexScan(tableRowCount AS INT64, indexCardinaity AS INT64) AS LOGIC
            IF tableRowCount < 1000
                RETURN FALSE // Sequential scan is faster for tiny tables
            ENDIF
            RETURN TRUE
    END CLASS

END NAMESPACE
