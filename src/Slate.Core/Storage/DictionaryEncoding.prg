// Storage/DictionaryEncoding.prg
// Dictionary encoding cardinality evaluation and prefix analysis in native X#

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.Core.Storage

    /// <summary>
    /// Dictionary cardinality estimation and compression viability in native X#.
    /// </summary>
    PUBLIC CLASS DictionaryAnalytics
        PUBLIC STATIC METHOD EstimateUniqueRatio(values AS STRING[]) AS REAL8
            IF values == NULL .OR. values:Length == 0
                RETURN 0.0
            ENDIF

            LOCAL set := HashSet<STRING>{StringComparer.Ordinal} AS HashSet<STRING>
            LOCAL i AS INT
            FOR i := 0 UPTO values:Length - 1
                IF values[i] != NULL
                    set:Add(values[i])
                ENDIF
            NEXT

            RETURN (REAL8)set:Count / (REAL8)values:Length

        PUBLIC STATIC METHOD ShouldUseDictionary(values AS STRING[]) AS LOGIC
            // If unique ratio is under 60%, dictionary encoding is advantageous
            LOCAL ratio := EstimateUniqueRatio(values) AS REAL8
            RETURN ratio < 0.60
    END CLASS

END NAMESPACE
