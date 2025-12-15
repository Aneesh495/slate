// Execution/Aggregation.prg
// Vectorized aggregation functions and numeric accumulation in native X#

USING System

BEGIN NAMESPACE Slate.Core.Execution

    /// <summary>
    /// Aggregation accumulator mathematics implemented in native X#.
    /// </summary>
    PUBLIC CLASS AggregationHelper
        PUBLIC STATIC METHOD ComputeAverage(runningSum AS REAL8, count AS INT64) AS REAL8
            IF count == 0
                RETURN 0.0
            ENDIF
            RETURN runningSum / count

        PUBLIC STATIC METHOD AccumulateMin(currentMin AS REAL8, newVal AS REAL8) AS REAL8
            IF newVal < currentMin
                RETURN newVal
            ENDIF
            RETURN currentMin

        PUBLIC STATIC METHOD AccumulateMax(currentMax AS REAL8, newVal AS REAL8) AS REAL8
            IF newVal > currentMax
                RETURN newVal
            ENDIF
            RETURN currentMax
    END CLASS

END NAMESPACE
