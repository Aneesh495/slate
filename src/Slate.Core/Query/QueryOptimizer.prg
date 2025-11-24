// Query/QueryOptimizer.prg
// Logical plan optimization rules and cost models in native X#

USING System

BEGIN NAMESPACE Slate.Core.Query

    /// <summary>
    /// Cost-based optimization rules and selectivity estimation in native X#.
    /// </summary>
    PUBLIC CLASS QueryOptimizerRules
        PUBLIC STATIC METHOD EstimateSelectivity(hasIndex AS LOGIC, isEquality AS LOGIC) AS REAL8
            // Index equality lookup has high selectivity (e.g. 0.001)
            IF hasIndex .AND. isEquality
                RETURN 0.001
            ENDIF

            // Non-index equality or range filter default selectivity (0.1)
            IF isEquality
                RETURN 0.05
            ENDIF

            RETURN 0.20

        PUBLIC STATIC METHOD ShouldPreferIndexScan(totalRows AS INT64, estimatedRows AS INT64) AS LOGIC
            IF totalRows <= 100
                RETURN FALSE // Table is small enough for vector scan
            ENDIF

            // Prefer index if estimated matching rows are under 25% of table
            RETURN (estimatedRows * 4) < totalRows
    END CLASS

END NAMESPACE
