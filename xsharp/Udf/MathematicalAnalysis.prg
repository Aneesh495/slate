// Udf/MathematicalAnalysis.prg
// Native X# user-defined analytics functions and mathematical linear regression kernels.

USING System

BEGIN NAMESPACE Slate.XSharp.Udf

    PUBLIC STATIC CLASS NativeMathAnalysis
        PUBLIC STATIC METHOD MovingAverage(series AS REAL8[], windowSize AS INT) AS REAL8[]
            IF series:Length == 0 .OR. windowSize <= 0
                RETURN REAL8[]{0}
            ENDIF

            LOCAL result := REAL8[]{series:Length} AS REAL8[]
            LOCAL runningSum := 0.0 AS REAL8

            FOR LOCAL i := 1 UPTO series:Length
                runningSum += series[i]
                IF i >= windowSize
                    IF i > windowSize
                        runningSum -= series[i - windowSize]
                    ENDIF
                    result[i] := runningSum / (REAL8)windowSize
                ELSE
                    result[i] := runningSum / (REAL8)i
                ENDIF
            NEXT

            RETURN result
    END CLASS

END NAMESPACE
