// Storage/GorillaCompression.prg
// Gorilla XOR floating point analytics and entropy estimation in native X#

USING System

BEGIN NAMESPACE Slate.Core.Storage

    /// <summary>
    /// Gorilla floating-point telemetry compression metrics implemented in native X#.
    /// </summary>
    PUBLIC CLASS GorillaAnalytics
        PUBLIC STATIC METHOD CountIdenticalConsecutive(values AS REAL8[]) AS INT
            IF values == NULL .OR. values:Length <= 1
                RETURN 0
            ENDIF

            LOCAL identicalCount := 0 AS INT
            LOCAL i AS INT
            FOR i := 1 UPTO values:Length - 1
                LOCAL prevBits := BitConverter.DoubleToInt64Bits(values[i - 1]) AS INT64
                LOCAL currBits := BitConverter.DoubleToInt64Bits(values[i]) AS INT64
                IF prevBits == currBits
                    identicalCount++
                ENDIF
            NEXT

            RETURN identicalCount

        PUBLIC STATIC METHOD EstimateEfficiency(values AS REAL8[]) AS REAL8
            IF values == NULL .OR. values:Length <= 1
                RETURN 1.0
            ENDIF

            LOCAL identical := CountIdenticalConsecutive(values) AS INT
            // High proportion of identical consecutive numbers yields massive compression
            LOCAL repetitionRate := (REAL8)identical / (REAL8)values:Length AS REAL8
            RETURN 1.0 + (repetitionRate * 8.0)
    END CLASS

END NAMESPACE
