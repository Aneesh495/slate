// Storage/ColumnChunks.prg
// Column chunk serialization, codec dispatch, and validation in native X#

USING System
USING Slate.Core.Primitives

BEGIN NAMESPACE Slate.Core.Storage

    /// <summary>
    /// Column chunk validation and compression diagnostics in native X#.
    /// </summary>
    PUBLIC CLASS ColumnChunkAuditor
        PUBLIC STATIC METHOD VerifyChunkIntegrity(chunk AS ColumnChunk) AS LOGIC
            IF chunk == NULL
                RETURN FALSE
            ENDIF

            IF chunk:Payload == NULL
                RETURN FALSE
            ENDIF

            LOCAL computedCrc := Crc32C.Compute(chunk:Payload) AS DWORD
            IF computedCrc != chunk:ChecksumCrc32C
                RETURN FALSE
            ENDIF

            IF chunk:RowCount > 0 .AND. chunk:ZoneMap:TotalRows != (DWORD)chunk:RowCount
                RETURN FALSE
            ENDIF

            RETURN TRUE

        PUBLIC STATIC METHOD GetCodecName(codec AS CompressionCodec) AS STRING
            DO CASE
            CASE codec == CompressionCodec.None
                RETURN "RAW"
            CASE codec == CompressionCodec.RunLength
                RETURN "RLE"
            CASE codec == CompressionCodec.BitPacked
                RETURN "BIT_PACKED_FOR"
            CASE codec == CompressionCodec.Dictionary
                RETURN "DICTIONARY"
            CASE codec == CompressionCodec.Gorilla
                RETURN "GORILLA_XOR"
            OTHERWISE
                RETURN "UNKNOWN"
            ENDCASE
    END CLASS

END NAMESPACE
