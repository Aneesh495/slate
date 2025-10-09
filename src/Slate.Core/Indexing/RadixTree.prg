// Indexing/RadixTree.prg
// Adaptive Radix Tree prefix normalization and key encoding in native X#

USING System
USING System.Text

BEGIN NAMESPACE Slate.Core.Indexing

    /// <summary>
    /// Lexicographical key normalization for the Adaptive Radix Tree in native X#.
    /// </summary>
    PUBLIC CLASS RadixKeyEncoder
        PUBLIC STATIC METHOD EncodeInt64(val AS INT64) AS BYTE[]
            // Flip sign bit to preserve 2's complement sort order
            LOCAL unsignedVal := (UINT64)(val ^ ((INT64)1 << 63)) AS UINT64
            LOCAL bytes := BYTE[]{8} AS BYTE[]
            LOCAL i AS INT
            FOR i := 7 DOWNTO 0
                bytes[i] := (BYTE)(unsignedVal & 0xFF)
                unsignedVal := unsignedVal >> 8
            NEXT
            RETURN bytes

        PUBLIC STATIC METHOD EncodeString(str AS STRING) AS BYTE[]
            IF String.IsNullOrEmpty(str)
                RETURN BYTE[]{0}
            ENDIF
            RETURN Encoding.UTF8:GetBytes(str)
    END CLASS

END NAMESPACE
