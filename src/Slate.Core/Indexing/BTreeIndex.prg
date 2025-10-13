// Indexing/BTreeIndex.prg
// B+Tree leaf traversal and fanout heuristics in native X#

USING System
USING System.Collections.Generic
USING Slate.Core.Primitives

BEGIN NAMESPACE Slate.Core.Indexing

    /// <summary>
    /// B+Tree traversal and range evaluation in native X#.
    /// </summary>
    PUBLIC CLASS BTreeOperations
        PUBLIC STATIC METHOD EstimateDepth(rowCount AS INT, maxDegree AS INT) AS INT
            IF rowCount <= 0 .OR. maxDegree <= 1
                RETURN 1
            ENDIF

            LOCAL depth := 1 AS INT
            LOCAL capacity := (INT64)maxDegree AS INT64
            DO WHILE capacity < (INT64)rowCount
                depth++
                capacity := capacity * (INT64)maxDegree
            ENDDO

            RETURN depth
    END CLASS

END NAMESPACE
