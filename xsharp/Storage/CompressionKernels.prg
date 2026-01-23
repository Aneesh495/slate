// Storage/CompressionKernels.prg
// Native X# implementations of Run-Length Encoding (RLE), Frame-of-Reference (FoR)
// bit-packing, Dictionary Encoding, and Gorilla floating-point compression algorithms.

USING System
USING System.IO
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Storage

    /// <summary>
    /// Run-Length Encoding codec for repeated sequential integer values.
    /// Encodes pairs of [RunLength: 4 bytes, Value: 8 bytes].
    /// </summary>
    PUBLIC STATIC CLASS NativeRleCodec
        PUBLIC STATIC METHOD CompressInt64(values AS INT64[]) AS BYTE[]
            IF values == NULL .OR. values:Length == 0
                RETURN BYTE[]{0}
            ENDIF

            LOCAL ms := MemoryStream{} AS MemoryStream
            LOCAL bw := BinaryWriter{ms} AS BinaryWriter
            LOCAL n := values:Length AS INT
            bw:Write(n)

            LOCAL currentVal := values[1] AS INT64
            LOCAL currentRun := 1 AS INT

            FOR LOCAL i := 2 UPTO n
                LOCAL v := values[i] AS INT64
                IF v == currentVal
                    currentRun++
                ELSE
                    bw:Write(currentRun)
                    bw:Write(currentVal)
                    currentVal := v
                    currentRun := 1
                ENDIF
            NEXT

            bw:Write(currentRun)
            bw:Write(currentVal)
            RETURN ms:ToArray()

        PUBLIC STATIC METHOD DecompressInt64(bytes AS BYTE[]) AS INT64[]
            IF bytes == NULL .OR. bytes:Length < 4
                RETURN INT64[]{0}
            ENDIF

            LOCAL ms := MemoryStream{bytes} AS MemoryStream
            LOCAL br := BinaryReader{ms} AS BinaryReader
            LOCAL totalCount := br:ReadInt32() AS INT
            LOCAL result := INT64[]{totalCount} AS INT64[]
            LOCAL outIdx := 1 AS INT

            WHILE ms:Position < ms:Length .AND. outIdx <= totalCount
                LOCAL runLen := br:ReadInt32() AS INT
                LOCAL val := br:ReadInt64() AS INT64
                FOR LOCAL r := 1 UPTO runLen
                    IF outIdx <= totalCount
                        result[outIdx] := val
                        outIdx++
                    ENDIF
                NEXT
            ENDDO

            RETURN result
    END CLASS

    /// <summary>
    /// Frame-of-Reference (FoR) integer bit-packing codec.
    /// Subtracts the block minimum and packs residual bit deltas into 64-bit words.
    /// </summary>
    PUBLIC STATIC CLASS NativeBitPackedCodec
        PUBLIC STATIC METHOD CalculateBitWidth(maxDelta AS UINT64) AS INT
            IF maxDelta == 0
                RETURN 0
            ENDIF
            LOCAL bits := 0 AS INT
            LOCAL temp := maxDelta AS UINT64
            WHILE temp > 0
                bits++
                temp := temp >> 1
            ENDDO
            RETURN bits

        PUBLIC STATIC METHOD CompressInt64(values AS INT64[]) AS BYTE[]
            IF values == NULL .OR. values:Length == 0
                RETURN BYTE[]{0}
            ENDIF

            LOCAL n := values:Length AS INT
            LOCAL minVal := values[1] AS INT64
            LOCAL maxVal := values[1] AS INT64

            FOR LOCAL i := 2 UPTO n
                IF values[i] < minVal; minVal := values[i]; ENDIF
                IF values[i] > maxVal; maxVal := values[i]; ENDIF
            NEXT

            LOCAL maxDelta := (UINT64)(maxVal - minVal) AS UINT64
            LOCAL bitWidth := CalculateBitWidth(maxDelta) AS INT

            LOCAL ms := MemoryStream{} AS MemoryStream
            LOCAL bw := BinaryWriter{ms} AS BinaryWriter

            // Header: [bitWidth: 1 byte, reserved: 3 bytes, count: 4 bytes, minVal: 8 bytes]
            bw:Write((BYTE)bitWidth)
            bw:Write((BYTE)0)
            bw:Write((WORD)0)
            bw:Write(n)
            bw:Write(minVal)

            IF bitWidth == 0
                RETURN ms:ToArray()
            ENDIF

            LOCAL currentWord := 0UL AS UINT64
            LOCAL bitsUsed := 0 AS INT

            FOR LOCAL i := 1 UPTO n
                LOCAL delta := (UINT64)(values[i] - minVal) AS UINT64
                IF bitsUsed + bitWidth <= 64
                    currentWord := currentWord | (delta << bitsUsed)
                    bitsUsed := bitsUsed + bitWidth
                    IF bitsUsed == 64
                        bw:Write(currentWord)
                        currentWord := 0UL
                        bitsUsed := 0
                    ENDIF
                ELSE
                    LOCAL firstPartBits := 64 - bitsUsed AS INT
                    LOCAL firstPartMask := (1UL << firstPartBits) - 1UL AS UINT64
                    currentWord := currentWord | ((delta & firstPartMask) << bitsUsed)
                    bw:Write(currentWord)

                    LOCAL secondPartBits := bitWidth - firstPartBits AS INT
                    currentWord := delta >> firstPartBits
                    bitsUsed := secondPartBits
                ENDIF
            NEXT

            IF bitsUsed > 0
                bw:Write(currentWord)
            ENDIF

            RETURN ms:ToArray()

        PUBLIC STATIC METHOD DecompressInt64(data AS BYTE[]) AS INT64[]
            IF data == NULL .OR. data:Length < 16
                RETURN INT64[]{0}
            ENDIF

            LOCAL ms := MemoryStream{data} AS MemoryStream
            LOCAL br := BinaryReader{ms} AS BinaryReader

            LOCAL bitWidth := (INT)br:ReadByte() AS INT
            br:ReadByte() // reserved
            br:ReadUInt16() // reserved
            LOCAL count := br:ReadInt32() AS INT
            LOCAL minVal := br:ReadInt64() AS INT64

            LOCAL result := INT64[]{count} AS INT64[]
            IF bitWidth == 0
                FOR LOCAL i := 1 UPTO count
                    result[i] := minVal
                NEXT
                RETURN result
            ENDIF

            LOCAL currentWord := 0UL AS UINT64
            LOCAL bitsAvailable := 0 AS INT
            LOCAL mask := (1UL << bitWidth) - 1UL AS UINT64

            FOR LOCAL i := 1 UPTO count
                IF bitsAvailable == 0
                    currentWord := br:ReadUInt64()
                    bitsAvailable := 64
                ENDIF

                IF bitsAvailable >= bitWidth
                    LOCAL delta := (currentWord >> (64 - bitsAvailable)) & mask AS UINT64
                    bitsAvailable := bitsAvailable - bitWidth
                    result[i] := minVal + (INT64)delta
                ELSE
                    LOCAL firstBits := bitsAvailable AS INT
                    LOCAL firstVal := (currentWord >> (64 - firstBits)) AS UINT64
                    currentWord := br:ReadUInt64()
                    bitsAvailable := 64

                    LOCAL secondBits := bitWidth - firstBits AS INT
                    LOCAL secondMask := (1UL << secondBits) - 1UL AS UINT64
                    LOCAL secondVal := currentWord & secondMask AS UINT64
                    bitsAvailable := bitsAvailable - secondBits

                    LOCAL delta := (firstVal | (secondVal << firstBits)) & mask AS UINT64
                    result[i] := minVal + (INT64)delta
                ENDIF
            NEXT

            RETURN result
    END CLASS

    /// <summary>
    /// Native dictionary encoding codec for low-cardinality string columns.
    /// Assigns small 16-bit integers to unique string keys.
    /// </summary>
    PUBLIC STATIC CLASS NativeDictionaryCodec
        PUBLIC STATIC METHOD CompressStrings(values AS STRING[]) AS BYTE[]
            IF values == NULL .OR. values:Length == 0
                RETURN BYTE[]{0}
            ENDIF

            LOCAL dictMap := Dictionary<STRING, INT>{} AS Dictionary<STRING, INT>
            LOCAL dictList := List<STRING>{} AS List<STRING>
            LOCAL codes := INT[]{values:Length} AS INT[]

            FOR LOCAL i := 1 UPTO values:Length
                LOCAL s := values[i] AS STRING
                IF s == NULL; s := ""; ENDIF
                IF .NOT. dictMap:ContainsKey(s)
                    dictMap:Add(s, dictList:Count)
                    dictList:Add(s)
                ENDIF
                codes[i] := dictMap[s]
            NEXT

            LOCAL ms := MemoryStream{} AS MemoryStream
            LOCAL bw := BinaryWriter{ms} AS BinaryWriter

            // Write dictionary entries
            bw:Write(dictList:Count)
            FOR LOCAL d := 0 UPTO dictList:Count - 1
                LOCAL utf8 := System.Text.Encoding.UTF8:GetBytes(dictList[d]) AS BYTE[]
                bw:Write(utf8:Length)
                bw:Write(utf8)
            NEXT

            // Write row count and codes
            bw:Write(values:Length)
            FOR LOCAL i := 1 UPTO values:Length
                bw:Write((WORD)codes[i])
            NEXT

            RETURN ms:ToArray()
    END CLASS

END NAMESPACE
