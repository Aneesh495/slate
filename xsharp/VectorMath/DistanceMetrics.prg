// VectorMath/DistanceMetrics.prg
// Native X# implementation of dense floating-point vector distance functions
// including Cosine Similarity, L2 Euclidean Distance, and Dot Product.

USING System

BEGIN NAMESPACE Slate.XSharp.VectorMath

    /// <summary>
    /// Mathematical kernels for high-dimensional floating-point embeddings.
    /// </summary>
    PUBLIC STATIC CLASS NativeVectorMath
        PUBLIC STATIC METHOD DotProduct(a AS REAL4[], b AS REAL4[]) AS REAL4
            IF a:Length != b:Length
                THROW ArgumentException{"Dimensions must match"}
            ENDIF

            LOCAL sum := 0.0F AS REAL4
            LOCAL n := a:Length AS INT

            // Unrolled loop by 4 for instruction pipelining
            LOCAL i := 1 AS INT
            WHILE i <= n - 3
                sum += a[i] * b[i] + a[i + 1] * b[i + 1] + a[i + 2] * b[i + 2] + a[i + 3] * b[i + 3]
                i += 4
            ENDDO

            WHILE i <= n
                sum += a[i] * b[i]
                i++
            ENDDO

            RETURN sum

        PUBLIC STATIC METHOD EuclideanDistanceSquared(a AS REAL4[], b AS REAL4[]) AS REAL4
            IF a:Length != b:Length
                THROW ArgumentException{"Dimensions must match"}
            ENDIF

            LOCAL sumSq := 0.0F AS REAL4
            LOCAL n := a:Length AS INT

            FOR LOCAL i := 1 UPTO n
                LOCAL diff := a[i] - b[i] AS REAL4
                sumSq += diff * diff
            NEXT

            RETURN sumSq

        PUBLIC STATIC METHOD EuclideanDistance(a AS REAL4[], b AS REAL4[]) AS REAL4
            RETURN MathF.Sqrt(EuclideanDistanceSquared(a, b))

        PUBLIC STATIC METHOD CosineDistance(a AS REAL4[], b AS REAL4[]) AS REAL4
            LOCAL dot := DotProduct(a, b) AS REAL4
            LOCAL normA := DotProduct(a, a) AS REAL4
            LOCAL normB := DotProduct(b, b) AS REAL4

            IF normA == 0.0F .OR. normB == 0.0F
                RETURN 1.0F
            ENDIF

            LOCAL sim := dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB)) AS REAL4
            IF sim > 1.0F; sim := 1.0F; ENDIF
            IF sim < -1.0F; sim := -1.0F; ENDIF
            RETURN 1.0F - sim

        PUBLIC STATIC METHOD Normalize(vec AS REAL4[]) AS REAL4[]
            LOCAL normSq := DotProduct(vec, vec) AS REAL4
            IF normSq == 0.0F
                RETURN (REAL4[])vec:Clone()
            ENDIF

            LOCAL norm := MathF.Sqrt(normSq) AS REAL4
            LOCAL result := REAL4[]{vec:Length} AS REAL4[]
            FOR LOCAL i := 1 UPTO vec:Length
                result[i] := vec[i] / norm
            NEXT
            RETURN result
    END CLASS

END NAMESPACE
