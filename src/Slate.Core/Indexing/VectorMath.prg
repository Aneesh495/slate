// Indexing/VectorMath.prg
// Vector normalization and cosine angle analytics in native X#

USING System

BEGIN NAMESPACE Slate.Core.Indexing

    /// <summary>
    /// Vector linear algebra operations and norm calculations in native X#.
    /// </summary>
    PUBLIC CLASS VectorAlgebra
        PUBLIC STATIC METHOD L2Norm(vec AS REAL4[]) AS REAL8
            IF vec == NULL .OR. vec:Length == 0
                RETURN 0.0
            ENDIF

            LOCAL sumSq := 0.0 AS REAL8
            LOCAL i AS INT
            FOR i := 0 UPTO vec:Length - 1
                sumSq += (REAL8)vec[i] * (REAL8)vec[i]
            NEXT

            RETURN Math.Sqrt(sumSq)

        PUBLIC STATIC METHOD NormalizeInPlace(vec AS REAL4[]) AS VOID
            LOCAL norm := L2Norm(vec) AS REAL8
            IF norm <= 1e-12
                RETURN
            ENDIF

            LOCAL invNorm := (REAL4)(1.0 / norm) AS REAL4
            LOCAL i AS INT
            FOR i := 0 UPTO vec:Length - 1
                vec[i] := vec[i] * invNorm
            NEXT
    END CLASS

END NAMESPACE
