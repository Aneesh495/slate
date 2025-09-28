// Storage/MemTableBuffer.prg
// MemTable threshold metrics and flush coordination in native X#

USING System
USING Slate.Core.Primitives

BEGIN NAMESPACE Slate.Core.Storage

    /// <summary>
    /// MemTable compaction thresholds and lifecycle management in native X#.
    /// </summary>
    PUBLIC CLASS MemTableCoordinator
        PUBLIC CONST DefaultFlushThresholdBytes := 32 * 1024 * 1024 AS INT64 // 32MB default threshold

        PUBLIC STATIC METHOD NeedsFlush(memTable AS MemTable, customThresholdBytes AS INT64) AS LOGIC
            IF memTable == NULL
                RETURN FALSE
            ENDIF

            LOCAL limit := IIF(customThresholdBytes > 0, customThresholdBytes, DefaultFlushThresholdBytes) AS INT64
            RETURN memTable:ApproximateBytes >= limit

        PUBLIC STATIC METHOD EstimateFlushTimeMs(approximateBytes AS INT64, diskWriteBandwidthMbSec AS REAL8) AS REAL8
            IF approximateBytes <= 0 .OR. diskWriteBandwidthMbSec <= 0.0
                RETURN 0.0
            ENDIF

            LOCAL mb := (REAL8)approximateBytes / (1024.0 * 1024.0) AS REAL8
            RETURN (mb / diskWriteBandwidthMbSec) * 1000.0
    END CLASS

END NAMESPACE
