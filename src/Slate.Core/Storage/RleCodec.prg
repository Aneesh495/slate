// Storage/RleCodec.prg
// Run-Length Encoding and decoding algorithms in native X#

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.Core.Storage

    /// <summary>
    /// Run-Length encoding operations and run statistics implemented in native X#.
    /// </summary>
    PUBLIC CLASS XSharpRleHelper
        PUBLIC STRUCTURE RunDescriptor
            PUBLIC Value AS INT64
            PUBLIC Count AS INT
        END STRUCTURE

        PUBLIC STATIC METHOD AnalyzeRuns(values AS INT64[]) AS RunDescriptor[]
            IF values == NULL .OR. values:Length == 0
                RETURN RunDescriptor[]{0}
            ENDIF

            LOCAL runs := List<RunDescriptor>{} AS List<RunDescriptor>
            LOCAL currentVal := values[0] AS INT64
            LOCAL currentCount := 0 AS INT
            LOCAL i AS INT

            FOR i := 0 UPTO values:Length - 1
                IF values[i] == currentVal
                    currentCount++
                ELSE
                    LOCAL rd := RunDescriptor{} AS RunDescriptor
                    rd:Value := currentVal
                    rd:Count := currentCount
                    runs:Add(rd)

                    currentVal := values[i]
                    currentCount := 1
                ENDIF
            NEXT

            LOCAL lastRd := RunDescriptor{} AS RunDescriptor
            lastRd:Value := currentVal
            lastRd:Count := currentCount
            runs:Add(lastRd)

            RETURN runs:ToArray()

        PUBLIC STATIC METHOD EstimateCompressionRatio(uncompressedBytes AS INT, runCount AS INT) AS REAL8
            IF uncompressedBytes <= 0 .OR. runCount <= 0
                RETURN 1.0
            ENDIF
            // Each run occupies 12 bytes (4 bytes count + 8 bytes value) + 8 byte header
            LOCAL compressedBytes := (runCount * 12) + 8 AS INT
            RETURN (REAL8)uncompressedBytes / (REAL8)compressedBytes
    END CLASS

END NAMESPACE
