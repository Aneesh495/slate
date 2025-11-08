// Query/XqlNodes.prg
// Extended Query Language (XQL) AST node classification in native X#

USING System

BEGIN NAMESPACE Slate.Core.Query

    /// <summary>
    /// XQL AST classification and node evaluation predicates in native X#.
    /// </summary>
    PUBLIC CLASS XqlNodeClassifier
        PUBLIC STATIC METHOD IsAggregateFunction(fnName AS STRING) AS LOGIC
            IF String.IsNullOrEmpty(fnName)
                RETURN FALSE
            ENDIF

            VAR upper := fnName:ToUpperInvariant()
            IF upper == "COUNT" .OR. upper == "SUM" .OR. upper == "AVG" .OR. ;
               upper == "MIN" .OR. upper == "MAX"
                RETURN TRUE
            ENDIF

            RETURN FALSE

        PUBLIC STATIC METHOD IsVectorDistanceMetric(metricName AS STRING) AS LOGIC
            IF String.IsNullOrEmpty(metricName)
                RETURN FALSE
            ENDIF

            VAR upper := metricName:ToUpperInvariant()
            IF upper == "L2" .OR. upper == "DOT_PRODUCT" .OR. upper == "COSINE"
                RETURN TRUE
            ENDIF

            RETURN FALSE
    END CLASS

END NAMESPACE
