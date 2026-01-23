// Storage/ColumnarStore.prg
// Native X# implementation of columnar block layout, memory-aligned framing,
// and vector batch serialization for the Slate HTAP database engine.

USING System
USING System.IO
USING System.Collections.Generic
USING System.Runtime.InteropServices

BEGIN NAMESPACE Slate.XSharp.Storage

    /// <summary>
    /// Magic 4-byte signature prefix for Slate Columnar Block binary files.
    /// Format: 'S' 'L' 'B' 'K' (0x4B424C53).
    /// </summary>
    PUBLIC STATIC CLASS StorageConstants
        PUBLIC CONST MagicHeader AS DWORD := 0x4B424C53
        PUBLIC CONST BlockAlignmentBytes AS INT := 64
        PUBLIC CONST DefaultBlockCapacity AS INT := 65536
        PUBLIC CONST MaxVectorDimension AS INT := 4096
    END CLASS

    /// <summary>
    /// Memory-aligned 64-byte block descriptor storing chunk telemetry,
    /// checksums, compression identifiers, and tuple cardinality.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack := 1)];
    PUBLIC STRUCTURE ColumnarBlockHeader
        PUBLIC Magic AS DWORD
        PUBLIC Version AS WORD
        PUBLIC Flags AS WORD
        PUBLIC ColumnId AS INT
        PUBLIC DataType AS BYTE
        PUBLIC CompressionKind AS BYTE
        PUBLIC Reserved1 AS WORD
        PUBLIC RowCount AS INT
        PUBLIC NullCount AS INT
        PUBLIC UncompressedBytes AS INT
        PUBLIC CompressedBytes AS INT
        PUBLIC MinInt64Value AS INT64
        PUBLIC MaxInt64Value AS INT64
        PUBLIC ChecksumCrc32C AS DWORD
        PUBLIC Reserved2 AS DWORD

        PUBLIC STATIC METHOD CreateEmpty() AS ColumnarBlockHeader
            LOCAL h AS ColumnarBlockHeader
            h := ColumnarBlockHeader{}
            h:Magic := StorageConstants.MagicHeader
            h:Version := 1
            h:Flags := 0
            h:ColumnId := 0
            h:DataType := 0
            h:CompressionKind := 0
            h:RowCount := 0
            h:NullCount := 0
            h:UncompressedBytes := 0
            h:CompressedBytes := 0
            h:MinInt64Value := 0
            h:MaxInt64Value := 0
            h:ChecksumCrc32C := 0
            RETURN h

        PUBLIC METHOD IsValid() AS LOGIC
            RETURN SELF:Magic == StorageConstants.MagicHeader .AND. SELF:Version == 1
    END STRUCTURE

    /// <summary>
    /// In-memory columnar vector representation holding raw typed elements
    /// and a bit-packed null mask.
    /// </summary>
    PUBLIC CLASS NativeColumnChunk
        PROTECT _header AS ColumnarBlockHeader
        PROTECT _nullBitmap AS BYTE[]
        PROTECT _payloadData AS BYTE[]
        PROTECT _rowCount AS INT

        PUBLIC PROPERTY Header AS ColumnarBlockHeader GET _header
        PUBLIC PROPERTY RowCount AS INT GET _rowCount
        PUBLIC PROPERTY Payload AS BYTE[] GET _payloadData

        PUBLIC CONSTRUCTOR(h AS ColumnarBlockHeader, payload AS BYTE[], nulls AS BYTE[])
            _header := h
            _payloadData := payload
            _nullBitmap := nulls
            _rowCount := h:RowCount
            RETURN

        PUBLIC METHOD IsNull(rowIdx AS INT) AS LOGIC
            IF _nullBitmap == NULL .OR. _nullBitmap:Length == 0
                RETURN FALSE
            ENDIF
            LOCAL byteOffset := rowIdx / 8 AS INT
            LOCAL bitOffset := rowIdx % 8 AS INT
            IF byteOffset >= _nullBitmap:Length
                RETURN FALSE
            ENDIF
            RETURN (_nullBitmap[byteOffset + 1] & (1 << bitOffset)) != 0

        PUBLIC METHOD SetNull(rowIdx AS INT, isNull AS LOGIC) AS VOID
            IF _nullBitmap == NULL
                LOCAL neededBytes := (_rowCount + 7) / 8 AS INT
                _nullBitmap := BYTE[]{neededBytes}
            ENDIF
            LOCAL byteOffset := rowIdx / 8 AS INT
            LOCAL bitOffset := rowIdx % 8 AS INT
            IF isNull
                _nullBitmap[byteOffset + 1] := (BYTE)(_nullBitmap[byteOffset + 1] | (1 << bitOffset))
            ELSE
                _nullBitmap[byteOffset + 1] := (BYTE)(_nullBitmap[byteOffset + 1] & ~(1 << bitOffset))
            ENDIF
            RETURN
    END CLASS

    /// <summary>
    /// Builder facility for assembling memory-aligned columnar chunks with automatic
    /// statistics computation and null tracking.
    /// </summary>
    PUBLIC CLASS NativeColumnChunkBuilder
        PROTECT _columnId AS INT
        PROTECT _dataType AS BYTE
        PROTECT _intValues AS List<INT64>
        PROTECT _floatValues AS List<REAL8>
        PROTECT _stringValues AS List<STRING>
        PROTECT _vectorValues AS List<REAL4[]>
        PROTECT _nullFlags AS List<LOGIC>

        PUBLIC CONSTRUCTOR(colId AS INT, dtype AS BYTE)
            _columnId := colId
            _dataType := dtype
            _intValues := List<INT64>{}
            _floatValues := List<REAL8>{}
            _stringValues := List<STRING>{}
            _vectorValues := List<REAL4[]>{}
            _nullFlags := List<LOGIC>{}
            RETURN

        PUBLIC METHOD AppendInt64(val AS INT64, isNull AS LOGIC) AS VOID
            _intValues:Add(val)
            _nullFlags:Add(isNull)
            RETURN

        PUBLIC METHOD AppendFloat64(val AS REAL8, isNull AS LOGIC) AS VOID
            _floatValues:Add(val)
            _nullFlags:Add(isNull)
            RETURN

        PUBLIC METHOD AppendString(val AS STRING, isNull AS LOGIC) AS VOID
            _stringValues:Add(val)
            _nullFlags:Add(isNull)
            RETURN

        PUBLIC METHOD AppendVector(val AS REAL4[], isNull AS LOGIC) AS VOID
            _vectorValues:Add(val)
            _nullFlags:Add(isNull)
            RETURN

        PUBLIC METHOD Build() AS NativeColumnChunk
            LOCAL h := ColumnarBlockHeader.CreateEmpty() AS ColumnarBlockHeader
            h:ColumnId := _columnId
            h:DataType := _dataType
            h:RowCount := _nullFlags:Count

            // Compute null counts
            LOCAL nullCount := 0 AS INT
            FOR LOCAL i := 0 UPTO _nullFlags:Count - 1
                IF _nullFlags[i]
                    nullCount++
                ENDIF
            NEXT
            h:NullCount := nullCount

            // Build null bitmask
            LOCAL nullBytesLen := (h:RowCount + 7) / 8 AS INT
            LOCAL nullBytes := BYTE[]{nullBytesLen}
            FOR LOCAL i := 0 UPTO _nullFlags:Count - 1
                IF _nullFlags[i]
                    LOCAL bIdx := i / 8 AS INT
                    LOCAL bitIdx := i % 8 AS INT
                    nullBytes[bIdx + 1] := (BYTE)(nullBytes[bIdx + 1] | (1 << bitIdx))
                ENDIF
            NEXT

            // Serialize payload according to type
            LOCAL ms := MemoryStream{} AS MemoryStream
            LOCAL bw := BinaryWriter{ms} AS BinaryWriter

            IF _dataType == 5 // Int64
                LOCAL minVal := INT64.MaxValue AS INT64
                LOCAL maxVal := INT64.MinValue AS INT64
                FOR LOCAL i := 0 UPTO _intValues:Count - 1
                    LOCAL v := _intValues[i] AS INT64
                    bw:Write(v)
                    IF .NOT. _nullFlags[i]
                        IF v < minVal; minVal := v; ENDIF
                        IF v > maxVal; maxVal := v; ENDIF
                    ENDIF
                NEXT
                h:MinInt64Value := minVal
                h:MaxInt64Value := maxVal
            ELSEIF _dataType == 7 // Float64
                FOR LOCAL i := 0 UPTO _floatValues:Count - 1
                    bw:Write(_floatValues[i])
                NEXT
            ELSEIF _dataType == 8 // String
                FOR LOCAL i := 0 UPTO _stringValues:Count - 1
                    LOCAL s := _stringValues[i] AS STRING
                    IF s == NULL; s := ""; ENDIF
                    LOCAL bytes := System.Text.Encoding.UTF8:GetBytes(s) AS BYTE[]
                    bw:Write((INT)bytes:Length)
                    bw:Write(bytes)
                NEXT
            ELSEIF _dataType == 12 // VectorFloat32
                FOR LOCAL i := 0 UPTO _vectorValues:Count - 1
                    LOCAL vec := _vectorValues[i] AS REAL4[]
                    IF vec == NULL
                        bw:Write((INT)0)
                    ELSE
                        bw:Write((INT)vec:Length)
                        FOR LOCAL d := 0 UPTO vec:Length - 1
                            bw:Write(vec[d + 1])
                        NEXT
                    ENDIF
                NEXT
            ENDIF

            LOCAL payload := ms:ToArray() AS BYTE[]
            h:UncompressedBytes := payload:Length
            h:CompressedBytes := payload:Length

            RETURN NativeColumnChunk{h, payload, nullBytes}
    END CLASS

END NAMESPACE
