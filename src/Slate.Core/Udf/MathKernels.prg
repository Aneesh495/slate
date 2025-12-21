// Udf/MathKernels.prg
// Statistical and mathematical routines in native X#

USING System

BEGIN NAMESPACE Slate.Core.Udf

    /// <summary>
    /// Mathematical and statistical analytics kernels implemented in native X#.
    /// </summary>
    PUBLIC CLASS XSharpMathKernels
        PUBLIC STATIC METHOD Sigmoid(x AS REAL8) AS REAL8
            RETURN 1.0 / (1.0 + Math.Exp(-x))

        PUBLIC STATIC METHOD ShannonEntropy2(p1 AS REAL8, p2 AS REAL8) AS REAL8
            VAR entropy := 0.0
            IF p1 > 0.0
                entropy := entropy - (p1 * Math.Log(p1, 2.0))
            ENDIF
            IF p2 > 0.0
                entropy := entropy - (p2 * Math.Log(p2, 2.0))
            ENDIF
            RETURN entropy

        PUBLIC STATIC METHOD ExponentialMovingAverageStep(currVal AS REAL8, prevEma AS REAL8, alpha AS REAL8) AS REAL8
            RETURN (alpha * currVal) + ((1.0 - alpha) * prevEma)
    END CLASS

END NAMESPACE
