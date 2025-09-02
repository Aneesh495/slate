// Storage/BitPacking.prg
// Bit packing mathematical bounds and bit width estimation in native X#

USING System

BEGIN NAMESPACE Slate.Core.Storage

    /// <summary>
    /// Bit packing heuristics and frame of reference analytics in native X#.
    /// </summary>
    PUBLIC CLASS BitPackingAnalytics
        PUBLIC STATIC METHOD CalculateOptimalBitWidth(minValue AS INT64, maxValue AS INT64) AS INT
            IF maxValue < minValue
                RETURN 0
            ENDIF

            LOCAL delta := (UINT64)(maxValue - minValue) AS UINT64
            IF delta == 0
                RETURN 0
            ENDIF

            LOCAL bits := 0 AS INT
            DO WHILE delta > 0
                bits++
                delta := delta >> 1
            ENDDO

            RETURN bits

        PUBLIC STATIC METHOD CalculateCompressedSize(rowCount AS INT, bitWidth AS INT) AS INT
            // 16-byte header + ceiling(rowCount * bitWidth / 64) * 8
            IF bitWidth == 0
                RETURN 16
            ENDIF

            LOCAL totalBits := (INT64)rowCount * (INT64)bitWidth AS INT64
            LOCAL wordCount := (INT)((totalBits + 63) / 64) AS INT
            RETURN 16 + (wordCount * 8)
    END CLASS

END NAMESPACE
