// Storage/BlockFraming.prg
// Binary block frame serialization and validation in native X#

USING System
USING System.IO

BEGIN NAMESPACE Slate.Core.Storage

    /// <summary>
    /// Frame validation and diagnostic helpers implemented in native X#.
    /// </summary>
    PUBLIC CLASS BlockFrameValidator
        PUBLIC CONST SlateMagic := 0x54414C53 AS DWORD

        PUBLIC STATIC METHOD ValidateHeader(header AS BlockHeader) AS LOGIC
            IF header:Magic != SlateMagic
                RETURN FALSE
            ENDIF

            IF header:Version != BlockHeader.CurrentVersion
                RETURN FALSE
            ENDIF

            IF header:CompressedBytes == 0 .AND. header:UncompressedBytes > 0
                RETURN FALSE
            ENDIF

            RETURN TRUE

        PUBLIC STATIC METHOD ReadFramedPayload(stream AS Stream, OUT header AS BlockHeader) AS BYTE[]
            LOCAL headerBuf := BYTE[]{64} AS BYTE[]
            LOCAL bytesRead := stream:Read(headerBuf, 0, 64) AS INT
            IF bytesRead < 64
                header := BlockHeader{}
                RETURN BYTE[]{0}
            ENDIF

            header := BlockHeader.FromByteArray(headerBuf)
            IF .NOT. ValidateHeader(header)
                RETURN BYTE[]{0}
            ENDIF

            LOCAL payload := BYTE[]{header:CompressedBytes} AS BYTE[]
            LOCAL totalRead := 0 AS INT
            DO WHILE totalRead < (INT)header:CompressedBytes
                LOCAL readChunk := stream:Read(payload, totalRead, (INT)header:CompressedBytes - totalRead) AS INT
                IF readChunk <= 0
                    EXIT
                ENDIF
                totalRead += readChunk
            ENDDO

            RETURN payload
    END CLASS

END NAMESPACE
