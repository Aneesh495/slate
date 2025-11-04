// Query/XqlTokenizer.prg
// Extended Query Language (XQL) token classification in native X#

USING System

BEGIN NAMESPACE Slate.Core.Query

    /// <summary>
    /// XQL Token and Operator classifier written in native X#.
    /// </summary>
    PUBLIC CLASS XqlTokenClassifier
        PUBLIC STATIC METHOD IsVectorOperator(opText AS STRING) AS LOGIC
            IF String.IsNullOrEmpty(opText)
                RETURN FALSE
            ENDIF

            // Vector distance operators: <-> (L2), <#> (Inner Product), <=> (Cosine)
            IF opText == "<->" .OR. opText == "<#>" .OR. opText == "<=>"
                RETURN TRUE
            ENDIF

            RETURN FALSE

        PUBLIC STATIC METHOD IsComparisonOperator(opText AS STRING) AS LOGIC
            IF String.IsNullOrEmpty(opText)
                RETURN FALSE
            ENDIF

            IF opText == "=" .OR. opText == "!=" .OR. opText == "<>" .OR. ;
               opText == "<" .OR. opText == "<=" .OR. opText == ">" .OR. opText == ">="
                RETURN TRUE
            ENDIF

            RETURN FALSE
    END CLASS

END NAMESPACE
