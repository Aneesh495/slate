// Execution/HashJoin.prg
// Hash join cost estimation and sizing logic in native X#

USING System

BEGIN NAMESPACE Slate.Core.Execution

    /// <summary>
    /// Hash join cost metrics and Bloom filter thresholding in native X#.
    /// </summary>
    PUBLIC CLASS HashJoinCostEstimator
        PUBLIC STATIC METHOD ChooseBuildSide(leftRowCount AS INT64, rightRowCount AS INT64) AS INT
            // Return 1 if left side is smaller, 2 if right side is smaller
            IF leftRowCount < rightRowCount
                RETURN 1
            ENDIF
            RETURN 2

        PUBLIC STATIC METHOD EstimateMemoryBytes(buildRowCount AS INT64, rowWidthBytes AS INT) AS INT64
            // Hash table overhead: approx 32 bytes per entry + row data
            RETURN buildRowCount * (rowWidthBytes + 32)
    END CLASS

END NAMESPACE
