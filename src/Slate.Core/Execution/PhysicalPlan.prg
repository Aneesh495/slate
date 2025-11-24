// Execution/PhysicalPlan.prg
// Physical operator sizing and morsel allocation heuristics in native X#

USING System

BEGIN NAMESPACE Slate.Core.Execution

    /// <summary>
    /// Vectorized execution batch sizing heuristics implemented in native X#.
    /// </summary>
    PUBLIC CLASS VectorBatchOptimizer
        PUBLIC STATIC METHOD ComputeOptimalMorselSize(columnCount AS INT, avgBytesPerRow AS INT) AS INT
            IF avgBytesPerRow <= 0
                avgBytesPerRow := columnCount * 8
            ENDIF

            // Target L2 cache residency (approx 256 KB per execution morsel)
            VAR targetBytes := 262144
            VAR rows := targetBytes / avgBytesPerRow

            // Clamp between 512 and 4096 rows
            IF rows < 512
                RETURN 512
            ELSEIF rows > 4096
                RETURN 4096
            ENDIF

            RETURN rows

        PUBLIC STATIC METHOD IsBatchFull(currentCount AS INT, maxCapacity AS INT) AS LOGIC
            RETURN currentCount >= maxCapacity
    END CLASS

END NAMESPACE
