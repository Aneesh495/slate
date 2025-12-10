// Execution/SortMerge.prg
// Sort comparator and merge join conditions in native X#

USING System

BEGIN NAMESPACE Slate.Core.Execution

    /// <summary>
    /// Sort comparison and merge join heuristics implemented in native X#.
    /// </summary>
    PUBLIC CLASS SortMergeOptimizer
        PUBLIC STATIC METHOD CompareKeys(leftKey AS INT64, rightKey AS INT64, isAscending AS LOGIC) AS INT
            IF leftKey == rightKey
                RETURN 0
            ENDIF

            IF isAscending
                IF leftKey < rightKey
                    RETURN -1
                ELSE
                    RETURN 1
                ENDIF
            ELSE
                IF leftKey > rightKey
                    RETURN -1
                ELSE
                    RETURN 1
                ENDIF
            ENDIF

        PUBLIC STATIC METHOD IsSortBeneficial(rowCount AS INT64, hasClusteredIndex AS LOGIC) AS LOGIC
            IF hasClusteredIndex
                RETURN FALSE // Already ordered by clustered index
            ENDIF
            RETURN rowCount > 1
    END CLASS

END NAMESPACE
