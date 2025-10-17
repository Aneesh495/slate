// Indexing/HnswGraph.prg
// HNSW graph topology analysis and recall metrics in native X#

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.Core.Indexing

    /// <summary>
    /// HNSW graph diagnostics and recall measurement in native X#.
    /// </summary>
    PUBLIC CLASS HnswEvaluator
        PUBLIC STATIC METHOD CalculateRecall(groundTruthIds AS INT[], predictedIds AS INT[]) AS REAL8
            IF groundTruthIds == NULL .OR. predictedIds == NULL .OR. groundTruthIds:Length == 0
                RETURN 0.0
            ENDIF

            LOCAL gtSet := HashSet<INT>{groundTruthIds} AS HashSet<INT>
            LOCAL matches := 0 AS INT

            LOCAL i AS INT
            FOR i := 0 UPTO predictedIds:Length - 1
                IF gtSet:Contains(predictedIds[i])
                    matches++
                ENDIF
            NEXT

            RETURN ((REAL8)matches / (REAL8)groundTruthIds:Length) * 100.0

        PUBLIC STATIC METHOD EstimateMemoryBytes(nodeCount AS INT, dimension AS INT, m AS INT) AS INT64
            // Per vector: dimension * 4 bytes
            // Per node: M * 4 bytes per neighbor list + graph object overhead
            LOCAL vectorBytes := (INT64)nodeCount * (INT64)dimension * 4L AS INT64
            LOCAL edgeBytes := (INT64)nodeCount * (INT64)m * 8L AS INT64
            LOCAL overheadBytes := (INT64)nodeCount * 64L AS INT64
            RETURN vectorBytes + edgeBytes + overheadBytes
    END CLASS

END NAMESPACE
