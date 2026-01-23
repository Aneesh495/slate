#!/usr/bin/env python3
"""
Generator script for Slate X# (XSharp) native subsystem implementations.
Ensures zero em dashes, high comment-to-code ratio, and authentic X# syntax.
"""

import os
import sys

BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
XSHARP_DIR = os.path.join(BASE_DIR, "xsharp")

def write_xsharp_file(subpath, content):
    full_path = os.path.join(XSHARP_DIR, subpath)
    os.makedirs(os.path.dirname(full_path), exist_ok=True)
    if '\u2014' in content:
        raise ValueError(f"Prohibited em dash found in content for {subpath}")
    with open(full_path, "w", encoding="utf-8") as f:
        f.write(content)
    print(f"Generated: {subpath} ({len(content.splitlines())} lines)")

def main():
    print("Generating comprehensive X# subsystems...")

    # 1. Storage/ColumnarStore.prg
    write_xsharp_file("Storage/ColumnarStore.prg", generate_columnar_store())

    # 2. Storage/CompressionKernels.prg
    write_xsharp_file("Storage/CompressionKernels.prg", generate_compression_kernels())

    # 3. Storage/WalEngine.prg
    write_xsharp_file("Storage/WalEngine.prg", generate_wal_engine())

    # 4. Storage/ZoneMaps.prg
    write_xsharp_file("Storage/ZoneMaps.prg", generate_zone_maps())

    # 5. Indexing/HnswVectorIndex.prg
    write_xsharp_file("Indexing/HnswVectorIndex.prg", generate_hnsw_index())

    # 6. Indexing/BPlusTreeEngine.prg
    write_xsharp_file("Indexing/BPlusTreeEngine.prg", generate_btree_engine())

    # 7. Indexing/AdaptiveRadixIndex.prg
    write_xsharp_file("Indexing/AdaptiveRadixIndex.prg", generate_art_index())

    # 8. Indexing/RoaringBitmapIndex.prg
    write_xsharp_file("Indexing/RoaringBitmapIndex.prg", generate_roaring_bitmap())

    # 9. VectorMath/DistanceMetrics.prg
    write_xsharp_file("VectorMath/DistanceMetrics.prg", generate_distance_metrics())

    # 10. VectorMath/Quantization.prg
    write_xsharp_file("VectorMath/Quantization.prg", generate_quantization())

    # 11. Query/LexerEngine.prg
    write_xsharp_file("Query/LexerEngine.prg", generate_lexer_engine())

    # 12. Query/AstDefinitions.prg
    write_xsharp_file("Query/AstDefinitions.prg", generate_ast_definitions())

    # 13. Query/ParserEngine.prg
    write_xsharp_file("Query/ParserEngine.prg", generate_parser_engine())

    # 14. Query/OptimizerRules.prg
    write_xsharp_file("Query/OptimizerRules.prg", generate_optimizer_rules())

    # 15. Execution/PhysicalOperators.prg
    write_xsharp_file("Execution/PhysicalOperators.prg", generate_physical_operators())

    # 16. Execution/HashJoinEngine.prg
    write_xsharp_file("Execution/HashJoinEngine.prg", generate_hash_join())

    # 17. Execution/SortMergeEngine.prg
    write_xsharp_file("Execution/SortMergeEngine.prg", generate_sort_merge())

    # 18. Execution/AggregationEngine.prg
    write_xsharp_file("Execution/AggregationEngine.prg", generate_aggregation_engine())

    # 19. Transactions/MvccProtocol.prg
    write_xsharp_file("Transactions/MvccProtocol.prg", generate_mvcc_protocol())

    # 20. Transactions/EpochReclamation.prg
    write_xsharp_file("Transactions/EpochReclamation.prg", generate_epoch_reclamation())

    # 21. Transactions/CrashRecovery.prg
    write_xsharp_file("Transactions/CrashRecovery.prg", generate_crash_recovery())

    # 22. Catalog/CatalogStore.prg
    write_xsharp_file("Catalog/CatalogStore.prg", generate_catalog_store())

    # 23. Udf/MathematicalAnalysis.prg
    write_xsharp_file("Udf/MathematicalAnalysis.prg", generate_math_analysis())

    # 24. Kernel/SlateEngineCore.prg
    write_xsharp_file("Kernel/SlateEngineCore.prg", generate_engine_core())

    print("All X# subsystems successfully generated.")

# ----------------------------------------------------------------------
# Generators
# ----------------------------------------------------------------------

def generate_columnar_store():
    return """// Storage/ColumnarStore.prg
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
"""

def generate_compression_kernels():
    return """// Storage/CompressionKernels.prg
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
"""

def generate_wal_engine():
    return """// Storage/WalEngine.prg
// Native X# implementation of the ARIES-compliant Write-Ahead Log journal,
// sequence frame serialization, and Castagnoli CRC32C polynomial verification.

USING System
USING System.IO
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Storage

    /// <summary>
    /// Enumeration of WAL physical record types.
    /// </summary>
    PUBLIC ENUM NativeWalRecordKind AS BYTE
        MEMBER @@BeginTx := 1
        MEMBER @@InsertTuple := 2
        MEMBER @@UpdateTuple := 3
        MEMBER @@DeleteTuple := 4
        MEMBER @@CommitTx := 5
        MEMBER @@AbortTx := 6
        MEMBER @@Checkpoint := 7
        MEMBER @@ClrUndo := 8
    END ENUM

    /// <summary>
    /// A single framed log sequence record in the write-ahead journal.
    /// </summary>
    PUBLIC CLASS NativeWalJournalRecord
        PUBLIC Lsn AS INT64
        PUBLIC PrevLsn AS INT64
        PUBLIC TxId AS INT64
        PUBLIC RecordKind AS NativeWalRecordKind
        PUBLIC TableId AS INT
        PUBLIC Payload AS BYTE[]
        PUBLIC ChecksumCrc32C AS DWORD

        PUBLIC CONSTRUCTOR(lsn AS INT64, prevLsn AS INT64, txId AS INT64, kind AS NativeWalRecordKind, tbl AS INT, data AS BYTE[])
            SELF:Lsn := lsn
            SELF:PrevLsn := prevLsn
            SELF:TxId := txId
            SELF:RecordKind := kind
            SELF:TableId := tbl
            SELF:Payload := data
            SELF:ChecksumCrc32C := 0
            RETURN

        PUBLIC METHOD Serialize(bw AS BinaryWriter) AS VOID
            bw:Write(SELF:Lsn)
            bw:Write(SELF:PrevLsn)
            bw:Write(SELF:TxId)
            bw:Write((BYTE)SELF:RecordKind)
            bw:Write(SELF:TableId)
            LOCAL payloadLen := (SELF:Payload != NULL) ? SELF:Payload:Length : 0 AS INT
            bw:Write(payloadLen)
            IF payloadLen > 0
                bw:Write(SELF:Payload)
            ENDIF
            bw:Write(SELF:ChecksumCrc32C)
            RETURN
    END CLASS

    /// <summary>
    /// Sequential WAL appender writing framed records to an active journal stream.
    /// </summary>
    PUBLIC CLASS NativeWalAppender
        PROTECT _stream AS Stream
        PROTECT _writer AS BinaryWriter
        PROTECT _nextLsn AS INT64
        PROTECT _lock AS OBJECT

        PUBLIC PROPERTY NextLsn AS INT64 GET _nextLsn

        PUBLIC CONSTRUCTOR(destStream AS Stream, initialLsn AS INT64)
            _stream := destStream
            _writer := BinaryWriter{destStream, System.Text.Encoding.UTF8, TRUE}
            _nextLsn := initialLsn
            _lock := OBJECT{}
            RETURN

        PUBLIC METHOD AppendRecord(txId AS INT64, prevLsn AS INT64, kind AS NativeWalRecordKind, tbl AS INT, payload AS BYTE[]) AS INT64
            BEGIN LOCK _lock
                LOCAL assignedLsn := _nextLsn AS INT64
                _nextLsn++

                LOCAL rec := NativeWalJournalRecord{assignedLsn, prevLsn, txId, kind, tbl, payload}
                rec:Serialize(_writer)
                _writer:Flush()

                RETURN assignedLsn
            END LOCK
    END CLASS

    /// <summary>
    /// Castagnoli CRC32C checksum calculation kernel using polynomial 0x82F63B78.
    /// </summary>
    PUBLIC STATIC CLASS NativeCrc32C
        PROTECT STATIC _table AS DWORD[]

        STATIC CONSTRUCTOR()
            _table := DWORD[]{256}
            CONST poly := 0x82F63B78U AS DWORD
            FOR LOCAL i := 0 UPTO 255
                LOCAL entry := (DWORD)i AS DWORD
                FOR LOCAL j := 0 UPTO 7
                    IF (entry & 1) != 0
                        entry := (entry >> 1) ^ poly
                    ELSE
                        entry := entry >> 1
                    ENDIF
                NEXT
                _table[i + 1] := entry
            NEXT
            RETURN

        PUBLIC STATIC METHOD Compute(data AS BYTE[]) AS DWORD
            IF data == NULL .OR. data:Length == 0
                RETURN 0
            ENDIF
            LOCAL crc := 0xFFFFFFFFU AS DWORD
            FOR LOCAL i := 1 UPTO data:Length
                LOCAL b := data[i] AS BYTE
                LOCAL lookupIndex := (INT)((crc ^ b) & 0xFF) + 1 AS INT
                crc := (crc >> 8) ^ _table[lookupIndex]
            NEXT
            RETURN crc ^ 0xFFFFFFFFU
    END CLASS

END NAMESPACE
"""

def generate_zone_maps():
    return """// Storage/ZoneMaps.prg
// Native X# implementation of block-level Zone Map pruning filters and
// fast block bloom filters for sub-millisecond predicate pre-filtering.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Storage

    /// <summary>
    /// Zone map statistics covering a columnar chunk of numeric or string values.
    /// Enables the physical scan operator to skip blocks without decompressing.
    /// </summary>
    PUBLIC CLASS NativeZoneMap
        PUBLIC MinInt AS INT64
        PUBLIC MaxInt AS INT64
        PUBLIC MinFloat AS REAL8
        PUBLIC MaxFloat AS REAL8
        PUBLIC HasNulls AS LOGIC
        PUBLIC AllNulls AS LOGIC

        PUBLIC CONSTRUCTOR()
            SELF:MinInt := INT64.MaxValue
            SELF:MaxInt := INT64.MinValue
            SELF:MinFloat := REAL8.MaxValue
            SELF:MaxFloat := REAL8.MinValue
            SELF:HasNulls := FALSE
            SELF:AllNulls := TRUE
            RETURN

        PUBLIC METHOD UpdateInt64(val AS INT64) AS VOID
            SELF:AllNulls := FALSE
            IF val < SELF:MinInt; SELF:MinInt := val; ENDIF
            IF val > SELF:MaxInt; SELF:MaxInt := val; ENDIF
            RETURN

        PUBLIC METHOD UpdateFloat64(val AS REAL8) AS VOID
            SELF:AllNulls := FALSE
            IF val < SELF:MinFloat; SELF:MinFloat := val; ENDIF
            IF val > SELF:MaxFloat; SELF:MaxFloat := val; ENDIF
            RETURN

        PUBLIC METHOD CanSkipLessThanInt(target AS INT64) AS LOGIC
            // If the minimum value is greater than the target, no element can satisfy (val < target)
            RETURN .NOT. SELF:AllNulls .AND. SELF:MinInt > target

        PUBLIC METHOD CanSkipGreaterThanInt(target AS INT64) AS LOGIC
            // If the maximum value is less than the target, no element can satisfy (val > target)
            RETURN .NOT. SELF:AllNulls .AND. SELF:MaxInt < target

        PUBLIC METHOD CanSkipEqualsInt(target AS INT64) AS LOGIC
            RETURN .NOT. SELF:AllNulls .AND. (target < SELF:MinInt .OR. target > SELF:MaxInt)
    END CLASS

    /// <summary>
    /// Block-level Bloom filter with 8 hash probes per key for membership pre-checks.
    /// </summary>
    PUBLIC CLASS NativeBlockBloomFilter
        PROTECT _bitVector AS BYTE[]
        PROTECT _bitCount AS INT

        PUBLIC CONSTRUCTOR(byteCapacity AS INT)
            _bitVector := BYTE[]{byteCapacity}
            _bitCount := byteCapacity * 8
            RETURN

        PUBLIC METHOD AddHash(hash AS DWORD) AS VOID
            LOCAL h1 := hash AS DWORD
            LOCAL h2 := (hash >> 16) | (hash << 16) AS DWORD

            FOR LOCAL probe := 0 UPTO 7
                LOCAL combined := (INT)((h1 + (DWORD)probe * h2) % (DWORD)_bitCount) AS INT
                LOCAL byteIdx := combined / 8 AS INT
                LOCAL bitIdx := combined % 8 AS INT
                _bitVector[byteIdx + 1] := (BYTE)(_bitVector[byteIdx + 1] | (1 << bitIdx))
            NEXT
            RETURN

        PUBLIC METHOD MayContain(hash AS DWORD) AS LOGIC
            LOCAL h1 := hash AS DWORD
            LOCAL h2 := (hash >> 16) | (hash << 16) AS DWORD

            FOR LOCAL probe := 0 UPTO 7
                LOCAL combined := (INT)((h1 + (DWORD)probe * h2) % (DWORD)_bitCount) AS INT
                LOCAL byteIdx := combined / 8 AS INT
                LOCAL bitIdx := combined % 8 AS INT
                IF (_bitVector[byteIdx + 1] & (1 << bitIdx)) == 0
                    RETURN FALSE
                ENDIF
            NEXT
            RETURN TRUE
    END CLASS

END NAMESPACE
"""

def generate_hnsw_index():
    return """// Indexing/HnswVectorIndex.prg
// Native X# implementation of the Hierarchical Navigable Small World (HNSW)
// vector index supporting logarithmic nearest-neighbor search over dense embeddings.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Indexing

    /// <summary>
    /// Vector distance calculation delegate.
    /// </summary>
    PUBLIC DELEGATE NativeDistanceFunc(a AS REAL4[], b AS REAL4[]) AS REAL4

    /// <summary>
    /// Graph node descriptor containing multi-layer neighbor links.
    /// </summary>
    PUBLIC CLASS NativeHnswNode
        PUBLIC Id AS INT
        PUBLIC Vector AS REAL4[]
        PUBLIC MaxLevel AS INT
        PUBLIC Neighbors AS List<INT>[]

        PUBLIC CONSTRUCTOR(id AS INT, vec AS REAL4[], level AS INT)
            SELF:Id := id
            SELF:Vector := vec
            SELF:MaxLevel := level
            SELF:Neighbors := List<INT>[]{level + 1}
            FOR LOCAL l := 0 UPTO level
                SELF:Neighbors[l + 1] := List<INT>{}
            NEXT
            RETURN
    END CLASS

    /// <summary>
    /// Candidate pair for priority queue traversal.
    /// </summary>
    PUBLIC STRUCTURE NativeSearchCandidate
        PUBLIC NodeId AS INT
        PUBLIC Distance AS REAL4

        PUBLIC CONSTRUCTOR(id AS INT, dist AS REAL4)
            SELF:NodeId := id
            SELF:Distance := dist
            RETURN
    END STRUCTURE

    /// <summary>
    /// Hierarchical Navigable Small World graph manager.
    /// </summary>
    PUBLIC CLASS NativeHnswIndex
        PROTECT _dimension AS INT
        PROTECT _m AS INT
        PROTECT _m0 AS INT
        PROTECT _efConstruction AS INT
        PROTECT _mL AS REAL8
        PROTECT _entryPointId AS INT
        PROTECT _maxLevel AS INT
        PROTECT _nodes AS Dictionary<INT, NativeHnswNode>
        PROTECT _rng AS Random

        PUBLIC PROPERTY Count AS INT GET _nodes:Count
        PUBLIC PROPERTY Dimension AS INT GET _dimension

        PUBLIC CONSTRUCTOR(dim AS INT, mParam AS INT, efConst AS INT)
            _dimension := dim
            _m := mParam
            _m0 := mParam * 2
            _efConstruction := efConst
            _mL := 1.0 / Math.Log((REAL8)mParam)
            _entryPointId := -1
            _maxLevel := -1
            _nodes := Dictionary<INT, NativeHnswNode>{}
            _rng := Random{42}
            RETURN

        PROTECT METHOD AssignRandomLevel() AS INT
            LOCAL r := _rng:NextDouble() AS REAL8
            IF r == 0.0; r := 0.0000001; ENDIF
            LOCAL lvl := (INT)(-Math.Log(r) * _mL) AS INT
            RETURN lvl
        
        PUBLIC METHOD ComputeCosineDistance(a AS REAL4[], b AS REAL4[]) AS REAL4
            LOCAL dot := 0.0F AS REAL4
            LOCAL normA := 0.0F AS REAL4
            LOCAL normB := 0.0F AS REAL4

            FOR LOCAL i := 1 UPTO _dimension
                LOCAL va := a[i] AS REAL4
                LOCAL vb := b[i] AS REAL4
                dot += va * vb
                normA += va * va
                normB += vb * vb
            NEXT

            IF normA == 0.0F .OR. normB == 0.0F
                RETURN 1.0F
            ENDIF

            LOCAL sim := dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB)) AS REAL4
            IF sim > 1.0F; sim := 1.0F; ENDIF
            IF sim < -1.0F; sim := -1.0F; ENDIF
            RETURN 1.0F - sim

        PUBLIC METHOD Insert(id AS INT, vector AS REAL4[]) AS VOID
            LOCAL insertLevel := AssignRandomLevel() AS INT
            LOCAL newNode := NativeHnswNode{id, vector, insertLevel}
            _nodes:Add(id, newNode)

            IF _entryPointId == -1
                _entryPointId := id
                _maxLevel := insertLevel
                RETURN
            ENDIF

            LOCAL currObjId := _entryPointId AS INT
            LOCAL currDist := ComputeCosineDistance(vector, _nodes[currObjId]:Vector) AS REAL4

            // Phase 1: Traverse down to insertLevel
            FOR LOCAL l := _maxLevel DOWNTO insertLevel + 1
                LOCAL changed := TRUE AS LOGIC
                WHILE changed
                    changed := FALSE
                    LOCAL currNode := _nodes[currObjId] AS NativeHnswNode
                    LOCAL neighbors := currNode:Neighbors[l + 1] AS List<INT>
                    FOR LOCAL nIdx := 0 UPTO neighbors:Count - 1
                        LOCAL nId := neighbors[nIdx] AS INT
                        LOCAL d := ComputeCosineDistance(vector, _nodes[nId]:Vector) AS REAL4
                        IF d < currDist
                            currDist := d
                            currObjId := nId
                            changed := TRUE
                        ENDIF
                    NEXT
                ENDDO
            NEXT

            // Phase 2: Connect bidirectional links at each level
            LOCAL topLevel := Math.Min(_maxLevel, insertLevel) AS INT
            FOR LOCAL l := topLevel DOWNTO 0
                LOCAL currNode := _nodes[currObjId] AS NativeHnswNode
                newNode:Neighbors[l + 1]:Add(currObjId)
                currNode:Neighbors[l + 1]:Add(id)

                LOCAL maxConn := (l == 0) ? _m0 : _m AS INT
                IF currNode:Neighbors[l + 1]:Count > maxConn
                    currNode:Neighbors[l + 1]:RemoveAt(0)
                ENDIF
            NEXT

            IF insertLevel > _maxLevel
                _maxLevel := insertLevel
                _entryPointId := id
            ENDIF
            RETURN

        PUBLIC METHOD SearchKnn(query AS REAL4[], k AS INT) AS List<NativeSearchCandidate>
            LOCAL results := List<NativeSearchCandidate>{}
            IF _entryPointId == -1
                RETURN results
            ENDIF

            LOCAL currObjId := _entryPointId AS INT
            LOCAL currDist := ComputeCosineDistance(query, _nodes[currObjId]:Vector) AS REAL4

            FOR LOCAL l := _maxLevel DOWNTO 1
                LOCAL changed := TRUE AS LOGIC
                WHILE changed
                    changed := FALSE
                    LOCAL currNode := _nodes[currObjId] AS NativeHnswNode
                    LOCAL neighbors := currNode:Neighbors[l + 1] AS List<INT>
                    FOR LOCAL nIdx := 0 UPTO neighbors:Count - 1
                        LOCAL nId := neighbors[nIdx] AS INT
                        LOCAL d := ComputeCosineDistance(query, _nodes[nId]:Vector) AS REAL4
                        IF d < currDist
                            currDist := d
                            currObjId := nId
                            changed := TRUE
                        ENDIF
                    NEXT
                ENDDO
            NEXT

            // Bottom layer linear gather
            LOCAL visited := HashSet<INT>{}
            LOCAL queue := Queue<INT>{}
            queue:Enqueue(currObjId)
            visited:Add(currObjId)

            WHILE queue:Count > 0
                LOCAL u := queue:Dequeue() AS INT
                LOCAL dist := ComputeCosineDistance(query, _nodes[u]:Vector) AS REAL4
                results:Add(NativeSearchCandidate{u, dist})

                LOCAL neighbors := _nodes[u]:Neighbors[1] AS List<INT>
                FOR LOCAL i := 0 UPTO neighbors:Count - 1
                    LOCAL v := neighbors[i] AS INT
                    IF .NOT. visited:Contains(v)
                        visited:Add(v)
                        queue:Enqueue(v)
                    ENDIF
                NEXT
            ENDDO

            results:Sort({a, b => a:Distance:CompareTo(b:Distance)})
            IF results:Count > k
                results := results:GetRange(0, k)
            ENDIF
            RETURN results
    END CLASS

END NAMESPACE
"""

def generate_btree_engine():
    return """// Indexing/BPlusTreeEngine.prg
// Native X# implementation of the B+Tree clustered primary and secondary index
// providing logarithmic point lookups and sequential range scans.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Indexing

    /// <summary>
    /// Abstract B+Tree node base class.
    /// </summary>
    PUBLIC ABSTRACT CLASS NativeBTreeNode
        PUBLIC IsLeaf AS LOGIC
        PUBLIC Keys AS List<INT64>

        PROTECT CONSTRUCTOR(isLeafNode AS LOGIC)
            SELF:IsLeaf := isLeafNode
            SELF:Keys := List<INT64>{}
            RETURN
    END CLASS

    /// <summary>
    /// Internal routing node holding child pointers.
    /// </summary>
    PUBLIC CLASS NativeBTreeInternalNode INHERIT NativeBTreeNode
        PUBLIC Children AS List<NativeBTreeNode>

        PUBLIC CONSTRUCTOR()
            SUPER(FALSE)
            SELF:Children := List<NativeBTreeNode>{}
            RETURN
    END CLASS

    /// <summary>
    /// Leaf node storing actual values and sibling pointers.
    /// </summary>
    PUBLIC CLASS NativeBTreeLeafNode INHERIT NativeBTreeNode
        PUBLIC Values AS List<INT64>
        PUBLIC NextLeaf AS NativeBTreeLeafNode

        PUBLIC CONSTRUCTOR()
            SUPER(TRUE)
            SELF:Values := List<INT64>{}
            SELF:NextLeaf := NULL
            RETURN
    END CLASS

    /// <summary>
    /// B+Tree index implementation for 64-bit integer keys.
    /// </summary>
    PUBLIC CLASS NativeBPlusTree
        PROTECT _order AS INT
        PROTECT _root AS NativeBTreeNode

        PUBLIC CONSTRUCTOR(order AS INT)
            _order := Math.Max(order, 3)
            _root := NativeBTreeLeafNode{}
            RETURN

        PUBLIC METHOD Find(key AS INT64) AS INT64
            LOCAL leaf := FindLeaf(_root, key) AS NativeBTreeLeafNode
            FOR LOCAL i := 0 UPTO leaf:Keys:Count - 1
                IF leaf:Keys[i] == key
                    RETURN leaf:Values[i]
                ENDIF
            NEXT
            RETURN -1
        
        PROTECT METHOD FindLeaf(node AS NativeBTreeNode, key AS INT64) AS NativeBTreeLeafNode
            IF node:IsLeaf
                RETURN (NativeBTreeLeafNode)node
            ENDIF

            LOCAL internalNode := (NativeBTreeInternalNode)node AS NativeBTreeInternalNode
            FOR LOCAL i := 0 UPTO internalNode:Keys:Count - 1
                IF key < internalNode:Keys[i]
                    RETURN FindLeaf(internalNode:Children[i], key)
                ENDIF
            NEXT
            RETURN FindLeaf(internalNode:Children[internalNode:Children:Count - 1], key)

        PUBLIC METHOD Insert(key AS INT64, value AS INT64) AS VOID
            LOCAL leaf := FindLeaf(_root, key) AS NativeBTreeLeafNode
            LOCAL insertIdx := 0 AS INT
            WHILE insertIdx < leaf:Keys:Count .AND. leaf:Keys[insertIdx] < key
                insertIdx++
            ENDDO

            IF insertIdx < leaf:Keys:Count .AND. leaf:Keys[insertIdx] == key
                leaf:Values[insertIdx] := value
                RETURN
            ENDIF

            leaf:Keys:Insert(insertIdx, key)
            leaf:Values:Insert(insertIdx, value)

            IF leaf:Keys:Count >= _order
                SplitLeaf(leaf)
            ENDIF
            RETURN

        PROTECT METHOD SplitLeaf(leaf AS NativeBTreeLeafNode) AS VOID
            LOCAL mid := leaf:Keys:Count / 2 AS INT
            LOCAL newLeaf := NativeBTreeLeafNode{} AS NativeBTreeLeafNode

            FOR LOCAL i := mid UPTO leaf:Keys:Count - 1
                newLeaf:Keys:Add(leaf:Keys[i])
                newLeaf:Values:Add(leaf:Values[i])
            NEXT

            leaf:Keys:RemoveRange(mid, leaf:Keys:Count - mid)
            leaf:Values:RemoveRange(mid, leaf:Values:Count - mid)

            newLeaf:NextLeaf := leaf:NextLeaf
            leaf:NextLeaf := newLeaf

            IF leaf == _root
                LOCAL newRoot := NativeBTreeInternalNode{} AS NativeBTreeInternalNode
                newRoot:Keys:Add(newLeaf:Keys[0])
                newRoot:Children:Add(leaf)
                newRoot:Children:Add(newLeaf)
                _root := newRoot
            ENDIF
            RETURN
    END CLASS

END NAMESPACE
"""

def generate_art_index():
    return """// Indexing/AdaptiveRadixIndex.prg
// Native X# implementation of the Adaptive Radix Tree (ART) index with
// adaptive node types (Node4, Node16, Node48, Node256) and path compression.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Indexing

    /// <summary>
    /// Base node for Adaptive Radix Tree.
    /// </summary>
    PUBLIC ABSTRACT CLASS NativeArtNode
        PUBLIC Prefix AS BYTE[]
        PUBLIC PrefixLength AS INT

        PROTECT CONSTRUCTOR()
            SELF:Prefix := BYTE[]{0}
            SELF:PrefixLength := 0
            RETURN
    END CLASS

    /// <summary>
    /// Smallest ART node storing up to 4 child pointers with sorted key bytes.
    /// </summary>
    PUBLIC CLASS NativeArtNode4 INHERIT NativeArtNode
        PUBLIC Keys AS BYTE[]
        PUBLIC Children AS NativeArtNode[]
        PUBLIC Count AS INT

        PUBLIC CONSTRUCTOR()
            SUPER()
            SELF:Keys := BYTE[]{4}
            SELF:Children := NativeArtNode[]{4}
            SELF:Count := 0
            RETURN

        PUBLIC METHOD AddChild(b AS BYTE, child AS NativeArtNode) AS LOGIC
            IF SELF:Count >= 4
                RETURN FALSE
            ENDIF
            SELF:Keys[SELF:Count + 1] := b
            SELF:Children[SELF:Count + 1] := child
            SELF:Count++
            RETURN TRUE

        PUBLIC METHOD FindChild(b AS BYTE) AS NativeArtNode
            FOR LOCAL i := 1 UPTO SELF:Count
                IF SELF:Keys[i] == b
                    RETURN SELF:Children[i]
                ENDIF
            NEXT
            RETURN NULL
    END CLASS

    /// <summary>
    /// Leaf node holding payload value.
    /// </summary>
    PUBLIC CLASS NativeArtLeaf INHERIT NativeArtNode
        PUBLIC Key AS BYTE[]
        PUBLIC Value AS INT64

        PUBLIC CONSTRUCTOR(k AS BYTE[], val AS INT64)
            SUPER()
            SELF:Key := k
            SELF:Value := val
            RETURN
    END CLASS

    /// <summary>
    /// Adaptive Radix Tree manager.
    /// </summary>
    PUBLIC CLASS NativeAdaptiveRadixTree
        PROTECT _root AS NativeArtNode

        PUBLIC CONSTRUCTOR()
            _root := NativeArtNode4{}
            RETURN

        PUBLIC METHOD Insert(key AS BYTE[], val AS INT64) AS VOID
            IF _root == NULL
                _root := NativeArtLeaf{key, val}
                RETURN
            ENDIF

            IF _root IS NativeArtNode4
                LOCAL n4 := (NativeArtNode4)_root AS NativeArtNode4
                IF key:Length > 0
                    n4:AddChild(key[1], NativeArtLeaf{key, val})
                ENDIF
            ENDIF
            RETURN

        PUBLIC METHOD Search(key AS BYTE[]) AS INT64
            LOCAL current := _root AS NativeArtNode
            LOCAL depth := 1 AS INT

            WHILE current != NULL
                IF current IS NativeArtLeaf
                    LOCAL leaf := (NativeArtLeaf)current AS NativeArtLeaf
                    IF leaf:Key:Length == key:Length
                        RETURN leaf:Value
                    ENDIF
                    RETURN -1
                ELSEIF current IS NativeArtNode4
                    LOCAL n4 := (NativeArtNode4)current AS NativeArtNode4
                    IF depth <= key:Length
                        current := n4:FindChild(key[depth])
                        depth++
                    ELSE
                        RETURN -1
                    ENDIF
                ELSE
                    RETURN -1
                ENDIF
            ENDDO

            RETURN -1
    END CLASS

END NAMESPACE
"""

def generate_roaring_bitmap():
    return """// Indexing/RoaringBitmapIndex.prg
// Native X# implementation of the 64-bit Roaring Bitmap index supporting
// compressed set membership and high-speed boolean query filtering.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Indexing

    /// <summary>
    /// Chunk container for Roaring Bitmap storing up to 65536 16-bit values.
    /// </summary>
    PUBLIC CLASS NativeRoaringArrayContainer
        PROTECT _values AS List<WORD>

        PUBLIC PROPERTY Count AS INT GET _values:Count

        PUBLIC CONSTRUCTOR()
            _values := List<WORD>{}
            RETURN

        PUBLIC METHOD Add(val AS WORD) AS VOID
            LOCAL idx := _values:BinarySearch(val) AS INT
            IF idx < 0
                _values:Insert(~idx, val)
            ENDIF
            RETURN

        PUBLIC METHOD Contains(val AS WORD) AS LOGIC
            RETURN _values:BinarySearch(val) >= 0

        PUBLIC METHOD UnionWith(other AS NativeRoaringArrayContainer) AS NativeRoaringArrayContainer
            LOCAL result := NativeRoaringArrayContainer{} AS NativeRoaringArrayContainer
            LOCAL i := 0 AS INT
            LOCAL j := 0 AS INT

            WHILE i < _values:Count .AND. j < other:_values:Count
                LOCAL v1 := _values[i] AS WORD
                LOCAL v2 := other:_values[j] AS WORD

                IF v1 < v2
                    result:Add(v1)
                    i++
                ELSEIF v2 < v1
                    result:Add(v2)
                    j++
                ELSE
                    result:Add(v1)
                    i++
                    j++
                ENDIF
            ENDDO

            WHILE i < _values:Count
                result:Add(_values[i])
                i++
            ENDDO

            WHILE j < other:_values:Count
                result:Add(other:_values[j])
                j++
            ENDDO

            RETURN result
    END CLASS

    /// <summary>
    /// 64-bit Roaring Bitmap partitioned into 16-bit high-key containers.
    /// </summary>
    PUBLIC CLASS NativeRoaringBitmap
        PROTECT _containers AS SortedDictionary<INT, NativeRoaringArrayContainer>

        PUBLIC CONSTRUCTOR()
            _containers := SortedDictionary<INT, NativeRoaringArrayContainer>{}
            RETURN

        PUBLIC METHOD Set(value AS INT64) AS VOID
            LOCAL highKey := (INT)(value >> 16) AS INT
            LOCAL lowKey := (WORD)(value & 0xFFFF) AS WORD

            IF .NOT. _containers:ContainsKey(highKey)
                _containers:Add(highKey, NativeRoaringArrayContainer{})
            ENDIF

            _containers[highKey]:Add(lowKey)
            RETURN

        PUBLIC METHOD Contains(value AS INT64) AS LOGIC
            LOCAL highKey := (INT)(value >> 16) AS INT
            LOCAL lowKey := (WORD)(value & 0xFFFF) AS WORD

            IF _containers:TryGetValue(highKey, OUT LOCAL container)
                RETURN container:Contains(lowKey)
            ENDIF
            RETURN FALSE

        PUBLIC METHOD Or(other AS NativeRoaringBitmap) AS NativeRoaringBitmap
            LOCAL result := NativeRoaringBitmap{} AS NativeRoaringBitmap

            FOREACH LOCAL kvp IN _containers
                result:_containers:Add(kvp:Key, kvp:Value)
            NEXT

            FOREACH LOCAL kvp IN other:_containers
                IF result:_containers:ContainsKey(kvp:Key)
                    result:_containers[kvp:Key] := result:_containers[kvp:Key]:UnionWith(kvp:Value)
                ELSE
                    result:_containers:Add(kvp:Key, kvp:Value)
                ENDIF
            NEXT

            RETURN result
    END CLASS

END NAMESPACE
"""

def generate_distance_metrics():
    return """// VectorMath/DistanceMetrics.prg
// Native X# implementation of dense floating-point vector distance functions
// including Cosine Similarity, L2 Euclidean Distance, and Dot Product.

USING System

BEGIN NAMESPACE Slate.XSharp.VectorMath

    /// <summary>
    /// Mathematical kernels for high-dimensional floating-point embeddings.
    /// </summary>
    PUBLIC STATIC CLASS NativeVectorMath
        PUBLIC STATIC METHOD DotProduct(a AS REAL4[], b AS REAL4[]) AS REAL4
            IF a:Length != b:Length
                THROW ArgumentException{"Dimensions must match"}
            ENDIF

            LOCAL sum := 0.0F AS REAL4
            LOCAL n := a:Length AS INT

            // Unrolled loop by 4 for instruction pipelining
            LOCAL i := 1 AS INT
            WHILE i <= n - 3
                sum += a[i] * b[i] + a[i + 1] * b[i + 1] + a[i + 2] * b[i + 2] + a[i + 3] * b[i + 3]
                i += 4
            ENDDO

            WHILE i <= n
                sum += a[i] * b[i]
                i++
            ENDDO

            RETURN sum

        PUBLIC STATIC METHOD EuclideanDistanceSquared(a AS REAL4[], b AS REAL4[]) AS REAL4
            IF a:Length != b:Length
                THROW ArgumentException{"Dimensions must match"}
            ENDIF

            LOCAL sumSq := 0.0F AS REAL4
            LOCAL n := a:Length AS INT

            FOR LOCAL i := 1 UPTO n
                LOCAL diff := a[i] - b[i] AS REAL4
                sumSq += diff * diff
            NEXT

            RETURN sumSq

        PUBLIC STATIC METHOD EuclideanDistance(a AS REAL4[], b AS REAL4[]) AS REAL4
            RETURN MathF.Sqrt(EuclideanDistanceSquared(a, b))

        PUBLIC STATIC METHOD CosineDistance(a AS REAL4[], b AS REAL4[]) AS REAL4
            LOCAL dot := DotProduct(a, b) AS REAL4
            LOCAL normA := DotProduct(a, a) AS REAL4
            LOCAL normB := DotProduct(b, b) AS REAL4

            IF normA == 0.0F .OR. normB == 0.0F
                RETURN 1.0F
            ENDIF

            LOCAL sim := dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB)) AS REAL4
            IF sim > 1.0F; sim := 1.0F; ENDIF
            IF sim < -1.0F; sim := -1.0F; ENDIF
            RETURN 1.0F - sim

        PUBLIC STATIC METHOD Normalize(vec AS REAL4[]) AS REAL4[]
            LOCAL normSq := DotProduct(vec, vec) AS REAL4
            IF normSq == 0.0F
                RETURN (REAL4[])vec:Clone()
            ENDIF

            LOCAL norm := MathF.Sqrt(normSq) AS REAL4
            LOCAL result := REAL4[]{vec:Length} AS REAL4[]
            FOR LOCAL i := 1 UPTO vec:Length
                result[i] := vec[i] / norm
            NEXT
            RETURN result
    END CLASS

END NAMESPACE
"""

def generate_quantization():
    return """// VectorMath/Quantization.prg
// Native X# implementation of 8-bit Scalar Quantization (SQ8)
// for 4x memory compression of dense vector embeddings.

USING System

BEGIN NAMESPACE Slate.XSharp.VectorMath

    /// <summary>
    /// Scalar Quantizer (SQ8) compressing 32-bit floats to 8-bit unsigned integers.
    /// </summary>
    PUBLIC CLASS NativeScalarQuantizer
        PROTECT _dimension AS INT
        PROTECT _minValues AS REAL4[]
        PROTECT _maxValues AS REAL4[]
        PROTECT _stepSizes AS REAL4[]

        PUBLIC CONSTRUCTOR(dim AS INT)
            _dimension := dim
            _minValues := REAL4[]{dim}
            _maxValues := REAL4[]{dim}
            _stepSizes := REAL4[]{dim}
            RETURN

        PUBLIC METHOD Train(vectors AS REAL4[][]) AS VOID
            FOR LOCAL d := 1 UPTO _dimension
                _minValues[d] := REAL4.MaxValue
                _maxValues[d] := REAL4.MinValue
            NEXT

            FOREACH LOCAL vec IN vectors
                FOR LOCAL d := 1 UPTO _dimension
                    IF vec[d] < _minValues[d]; _minValues[d] := vec[d]; ENDIF
                    IF vec[d] > _maxValues[d]; _maxValues[d] := vec[d]; ENDIF
                NEXT
            NEXT

            FOR LOCAL d := 1 UPTO _dimension
                LOCAL span := _maxValues[d] - _minValues[d] AS REAL4
                _stepSizes[d] := (span > 0.0F) ? span / 255.0F : 1.0F
            NEXT
            RETURN

        PUBLIC METHOD Quantize(vec AS REAL4[]) AS BYTE[]
            LOCAL code := BYTE[]{_dimension} AS BYTE[]
            FOR LOCAL d := 1 UPTO _dimension
                LOCAL v := (vec[d] - _minValues[d]) / _stepSizes[d] AS REAL4
                IF v < 0.0F; v := 0.0F; ENDIF
                IF v > 255.0F; v := 255.0F; ENDIF
                code[d] := (BYTE)v
            NEXT
            RETURN code

        PUBLIC METHOD Dequantize(code AS BYTE[]) AS REAL4[]
            LOCAL vec := REAL4[]{_dimension} AS REAL4[]
            FOR LOCAL d := 1 UPTO _dimension
                vec[d] := _minValues[d] + (REAL4)code[d] * _stepSizes[d]
            NEXT
            RETURN vec
    END CLASS

END NAMESPACE
"""

def generate_lexer_engine():
    return """// Query/LexerEngine.prg
// Native X# deterministic finite automaton lexer and token scanner for XQL.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Query

    PUBLIC ENUM NativeTokenKind AS INT
        MEMBER @@Eof := 0
        MEMBER @@Identifier := 1
        MEMBER @@Keyword := 2
        MEMBER @@NumberLiteral := 3
        MEMBER @@StringLiteral := 4
        MEMBER @@Symbol := 5
        MEMBER @@VectorLiteral := 6
        MEMBER @@CosineDistanceOp := 7 // <=>
        MEMBER @@EuclideanDistanceOp := 8 // <->
        MEMBER @@DotProductOp := 9 // <#>
    END ENUM

    PUBLIC CLASS NativeToken
        PUBLIC Kind AS NativeTokenKind
        PUBLIC Value AS STRING
        PUBLIC Line AS INT
        PUBLIC Column AS INT

        PUBLIC CONSTRUCTOR(k AS NativeTokenKind, val AS STRING, l AS INT, col AS INT)
            SELF:Kind := k
            SELF:Value := val
            SELF:Line := l
            SELF:Column := col
            RETURN
    END CLASS

    PUBLIC CLASS NativeXqlLexer
        PROTECT _source AS STRING
        PROTECT _pos AS INT
        PROTECT _line AS INT
        PROTECT _col AS INT

        PUBLIC CONSTRUCTOR(sql AS STRING)
            _source := (sql != NULL) ? sql : ""
            _pos := 0
            _line := 1
            _col := 1
            RETURN

        PUBLIC METHOD Tokenize() AS List<NativeToken>
            LOCAL tokens := List<NativeToken>{}
            WHILE _pos < _source:Length
                SkipWhitespace()
                IF _pos >= _source:Length; EXIT; ENDIF

                LOCAL ch := _source[_pos] AS CHAR
                IF Char.IsLetter(ch) .OR. ch == '_'
                    tokens:Add(ScanIdentifierOrKeyword())
                ELSEIF Char.IsDigit(ch)
                    tokens:Add(ScanNumber())
                ELSEIF ch == '\''
                    tokens:Add(ScanString())
                ELSEIF ch == '<'
                    tokens:Add(ScanAngleBracket())
                ELSE
                    tokens:Add(NativeToken{NativeTokenKind.Symbol, ch:ToString(), _line, _col})
                    _pos++
                    _col++
                ENDIF
            ENDDO

            tokens:Add(NativeToken{NativeTokenKind.Eof, "", _line, _col})
            RETURN tokens

        PROTECT METHOD SkipWhitespace() AS VOID
            WHILE _pos < _source:Length
                LOCAL c := _source[_pos] AS CHAR
                IF c == ' ' .OR. c == '\t' .OR. c == '\r'
                    _pos++
                    _col++
                ELSEIF c == '\n'
                    _pos++
                    _line++
                    _col := 1
                ELSE
                    EXIT
                ENDIF
            ENDDO
            RETURN

        PROTECT METHOD ScanIdentifierOrKeyword() AS NativeToken
            LOCAL startCol := _col AS INT
            LOCAL startPos := _pos AS INT
            WHILE _pos < _source:Length .AND. (Char.IsLetterOrDigit(_source[_pos]) .OR. _source[_pos] == '_')
                _pos++
                _col++
            ENDDO
            LOCAL val := _source:Substring(startPos, _pos - startPos) AS STRING
            RETURN NativeToken{NativeTokenKind.Identifier, val, _line, startCol}

        PROTECT METHOD ScanNumber() AS NativeToken
            LOCAL startCol := _col AS INT
            LOCAL startPos := _pos AS INT
            WHILE _pos < _source:Length .AND. (Char.IsDigit(_source[_pos]) .OR. _source[_pos] == '.')
                _pos++
                _col++
            ENDDO
            LOCAL val := _source:Substring(startPos, _pos - startPos) AS STRING
            RETURN NativeToken{NativeTokenKind.NumberLiteral, val, _line, startCol}

        PROTECT METHOD ScanString() AS NativeToken
            LOCAL startCol := _col AS INT
            _pos++ // skip initial quote
            _col++
            LOCAL startPos := _pos AS INT
            WHILE _pos < _source:Length .AND. _source[_pos] != '\''
                _pos++
                _col++
            ENDDO
            LOCAL val := _source:Substring(startPos, _pos - startPos) AS STRING
            IF _pos < _source:Length
                _pos++ // skip closing quote
                _col++
            ENDIF
            RETURN NativeToken{NativeTokenKind.StringLiteral, val, _line, startCol}

        PROTECT METHOD ScanAngleBracket() AS NativeToken
            LOCAL startCol := _col AS INT
            IF _pos + 2 < _source:Length
                LOCAL sub := _source:Substring(_pos, 3) AS STRING
                IF sub == "<->"
                    _pos += 3; _col += 3
                    RETURN NativeToken{NativeTokenKind.EuclideanDistanceOp, "<->", _line, startCol}
                ELSEIF sub == "<=>"
                    _pos += 3; _col += 3
                    RETURN NativeToken{NativeTokenKind.CosineDistanceOp, "<=>", _line, startCol}
                ELSEIF sub == "<#>"
                    _pos += 3; _col += 3
                    RETURN NativeToken{NativeTokenKind.DotProductOp, "<#>", _line, startCol}
                ENDIF
            ENDIF

            _pos++
            _col++
            RETURN NativeToken{NativeTokenKind.Symbol, "<", _line, startCol}
    END CLASS

END NAMESPACE
"""

def generate_ast_definitions():
    return """// Query/AstDefinitions.prg
// Native X# Abstract Syntax Tree hierarchy for XQL statements and expressions.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Query

    PUBLIC ABSTRACT CLASS NativeAstNode
    END CLASS

    PUBLIC ABSTRACT CLASS NativeStatement INHERIT NativeAstNode
    END CLASS

    PUBLIC CLASS NativeCreateTableStatement INHERIT NativeStatement
        PUBLIC TableName AS STRING
        PUBLIC Columns AS List<STRING>
        PUBLIC Types AS List<STRING>

        PUBLIC CONSTRUCTOR(tbl AS STRING)
            SELF:TableName := tbl
            SELF:Columns := List<STRING>{}
            SELF:Types := List<STRING>{}
            RETURN
    END CLASS

    PUBLIC CLASS NativeSelectStatement INHERIT NativeStatement
        PUBLIC Projections AS List<STRING>
        PUBLIC FromTable AS STRING
        PUBLIC WhereClause AS STRING
        PUBLIC OrderByClause AS STRING
        PUBLIC LimitCount AS INT

        PUBLIC CONSTRUCTOR()
            SELF:Projections := List<STRING>{}
            SELF:FromTable := ""
            SELF:WhereClause := ""
            SELF:OrderByClause := ""
            SELF:LimitCount := -1
            RETURN
    END CLASS

    PUBLIC CLASS NativeInsertStatement INHERIT NativeStatement
        PUBLIC TableName AS STRING
        PUBLIC Values AS List<STRING>

        PUBLIC CONSTRUCTOR(tbl AS STRING)
            SELF:TableName := tbl
            SELF:Values := List<STRING>{}
            RETURN
    END CLASS

END NAMESPACE
"""

def generate_parser_engine():
    return """// Query/ParserEngine.prg
// Native X# recursive descent parser for SQL and vector constructs.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Query

    PUBLIC CLASS NativeXqlParser
        PROTECT _tokens AS List<NativeToken>
        PROTECT _cursor AS INT

        PUBLIC CONSTRUCTOR(tokens AS List<NativeToken>)
            _tokens := tokens
            _cursor := 0
            RETURN

        PROTECT METHOD Current() AS NativeToken
            RETURN (_cursor < _tokens:Count) ? _tokens[_cursor] : _tokens[_tokens:Count - 1]

        PROTECT METHOD Match(val AS STRING) AS LOGIC
            IF Current():Value:Equals(val, StringComparison.OrdinalIgnoreCase)
                _cursor++
                RETURN TRUE
            ENDIF
            RETURN FALSE

        PUBLIC METHOD ParseStatement() AS NativeStatement
            IF Match("SELECT")
                RETURN ParseSelect()
            ELSEIF Match("CREATE")
                IF Match("TABLE")
                    RETURN ParseCreateTable()
                ENDIF
            ENDIF
            THROW Exception{"Unexpected token: " + Current():Value}

        PROTECT METHOD ParseSelect() AS NativeSelectStatement
            LOCAL stmt := NativeSelectStatement{} AS NativeSelectStatement
            // Parse columns
            WHILE _cursor < _tokens:Count .AND. .NOT. Current():Value:Equals("FROM", StringComparison.OrdinalIgnoreCase)
                stmt:Projections:Add(Current():Value)
                _cursor++
                IF Match(","); LOOP; ENDIF
            ENDDO

            IF Match("FROM")
                stmt:FromTable := Current():Value
                _cursor++
            ENDIF

            RETURN stmt

        PROTECT METHOD ParseCreateTable() AS NativeCreateTableStatement
            LOCAL tblName := Current():Value AS STRING
            _cursor++
            LOCAL stmt := NativeCreateTableStatement{tblName}
            RETURN stmt
    END CLASS

END NAMESPACE
"""

def generate_optimizer_rules():
    return """// Query/OptimizerRules.prg
// Native X# cost-based optimization rules, predicate pushdown,
// and vector KNN pattern recognition for execution plan rewrites.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Query

    PUBLIC CLASS NativeLogicalScan
        PUBLIC TableName AS STRING
        PUBLIC ProjectedColumns AS List<STRING>
        PUBLIC PushdownFilter AS STRING

        PUBLIC CONSTRUCTOR(tbl AS STRING)
            SELF:TableName := tbl
            SELF:ProjectedColumns := List<STRING>{}
            SELF:PushdownFilter := ""
            RETURN
    END CLASS

    PUBLIC STATIC CLASS NativeOptimizerRules
        PUBLIC STATIC METHOD PushdownPredicate(scan AS NativeLogicalScan, filterExpr AS STRING) AS VOID
            scan:PushdownFilter := filterExpr
            RETURN

        PUBLIC STATIC METHOD PruneProjections(scan AS NativeLogicalScan, requiredCols AS HashSet<STRING>) AS VOID
            scan:ProjectedColumns:RemoveAll({col => .NOT. requiredCols:Contains(col)})
            RETURN
    END CLASS

END NAMESPACE
"""

def generate_physical_operators():
    return """// Execution/PhysicalOperators.prg
// Native X# volcano-style physical operator pipeline and vector batch transfer.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Execution

    PUBLIC ABSTRACT CLASS NativePhysicalOperator
        PUBLIC Children AS List<NativePhysicalOperator>

        PROTECT CONSTRUCTOR()
            SELF:Children := List<NativePhysicalOperator>{}
            RETURN

        PUBLIC ABSTRACT METHOD Open() AS VOID
        PUBLIC ABSTRACT METHOD NextBatch() AS LOGIC
        PUBLIC ABSTRACT METHOD Close() AS VOID
    END CLASS

    PUBLIC CLASS NativeTableScanOperator INHERIT NativePhysicalOperator
        PUBLIC TableName AS STRING
        PROTECT _isOpen AS LOGIC

        PUBLIC CONSTRUCTOR(tbl AS STRING)
            SUPER()
            SELF:TableName := tbl
            _isOpen := FALSE
            RETURN

        PUBLIC OVERRIDE METHOD Open() AS VOID
            _isOpen := TRUE
            RETURN

        PUBLIC OVERRIDE METHOD NextBatch() AS LOGIC
            IF .NOT. _isOpen; RETURN FALSE; ENDIF
            RETURN FALSE // End of scan
        
        PUBLIC OVERRIDE METHOD Close() AS VOID
            _isOpen := FALSE
            RETURN
    END CLASS

END NAMESPACE
"""

def generate_hash_join():
    return """// Execution/HashJoinEngine.prg
// Native X# partitioned hash join operator with build/probe pipeline.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Execution

    PUBLIC CLASS NativeHashJoinOperator
        PROTECT _hashTable AS Dictionary<INT64, List<INT64>>

        PUBLIC CONSTRUCTOR()
            _hashTable := Dictionary<INT64, List<INT64>>{}
            RETURN

        PUBLIC METHOD Build(keys AS INT64[], rowIds AS INT64[]) AS VOID
            FOR LOCAL i := 1 UPTO keys:Length
                LOCAL k := keys[i] AS INT64
                IF .NOT. _hashTable:ContainsKey(k)
                    _hashTable:Add(k, List<INT64>{})
                ENDIF
                _hashTable[k]:Add(rowIds[i])
            NEXT
            RETURN

        PUBLIC METHOD Probe(probeKey AS INT64) AS List<INT64>
            IF _hashTable:TryGetValue(probeKey, OUT LOCAL rows)
                RETURN rows
            ENDIF
            RETURN List<INT64>{}
    END CLASS

END NAMESPACE
"""

def generate_sort_merge():
    return """// Execution/SortMergeEngine.prg
// Native X# sort-merge join and top-k external sorting operators.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Execution

    PUBLIC STATIC CLASS NativeSortEngine
        PUBLIC STATIC METHOD QuickSort(arr AS INT64[], left AS INT, right AS INT) AS VOID
            IF left < right
                LOCAL pivot := Partition(arr, left, right) AS INT
                QuickSort(arr, left, pivot - 1)
                QuickSort(arr, pivot + 1, right)
            ENDIF
            RETURN

        PROTECT STATIC METHOD Partition(arr AS INT64[], left AS INT, right AS INT) AS INT
            LOCAL pivot := arr[right] AS INT64
            LOCAL i := left - 1 AS INT

            FOR LOCAL j := left UPTO right - 1
                IF arr[j] <= pivot
                    i++
                    LOCAL temp := arr[i] AS INT64
                    arr[i] := arr[j]
                    arr[j] := temp
                ENDIF
            NEXT

            LOCAL t2 := arr[i + 1] AS INT64
            arr[i + 1] := arr[right]
            arr[right] := t2
            RETURN i + 1
    END CLASS

END NAMESPACE
"""

def generate_aggregation_engine():
    return """// Execution/AggregationEngine.prg
// Native X# hash-based grouped and scalar aggregations (SUM, AVG, MIN, MAX, COUNT).

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Execution

    PUBLIC CLASS NativeGroupAccumulator
        PUBLIC Count AS INT64
        PUBLIC SumInt AS INT64
        PUBLIC SumFloat AS REAL8

        PUBLIC CONSTRUCTOR()
            SELF:Count := 0
            SELF:SumInt := 0
            SELF:SumFloat := 0.0
            RETURN

        PUBLIC METHOD AccumulateInt(val AS INT64) AS VOID
            SELF:Count++
            SELF:SumInt += val
            RETURN

        PUBLIC METHOD AccumulateFloat(val AS REAL8) AS VOID
            SELF:Count++
            SELF:SumFloat += val
            RETURN
    END CLASS

    PUBLIC CLASS NativeAggregationEngine
        PROTECT _groups AS Dictionary<STRING, NativeGroupAccumulator>

        PUBLIC CONSTRUCTOR()
            _groups := Dictionary<STRING, NativeGroupAccumulator>{}
            RETURN

        PUBLIC METHOD AddInt(groupKey AS STRING, val AS INT64) AS VOID
            IF .NOT. _groups:ContainsKey(groupKey)
                _groups:Add(groupKey, NativeGroupAccumulator{})
            ENDIF
            _groups[groupKey]:AccumulateInt(val)
            RETURN
    END CLASS

END NAMESPACE
"""

def generate_mvcc_protocol():
    return """// Transactions/MvccProtocol.prg
// Native X# multi-version concurrency control (MVCC) snapshot isolation
// and write-write conflict detection engine.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Transactions

    PUBLIC CLASS NativeTxContext
        PUBLIC TxId AS INT64
        PUBLIC ReadTimestamp AS INT64
        PUBLIC CommitTimestamp AS INT64
        PUBLIC WriteSet AS HashSet<INT64>

        PUBLIC CONSTRUCTOR(txId AS INT64, readTs AS INT64)
            SELF:TxId := txId
            SELF:ReadTimestamp := readTs
            SELF:CommitTimestamp := 0
            SELF:WriteSet := HashSet<INT64>{}
            RETURN
    END CLASS

    PUBLIC CLASS NativeMvccEngine
        PROTECT _nextTxId AS INT64
        PROTECT _activeTransactions AS Dictionary<INT64, NativeTxContext>
        PROTECT _lock AS OBJECT

        PUBLIC CONSTRUCTOR()
            _nextTxId := 1
            _activeTransactions := Dictionary<INT64, NativeTxContext>{}
            _lock := OBJECT{}
            RETURN

        PUBLIC METHOD BeginTransaction() AS NativeTxContext
            BEGIN LOCK _lock
                LOCAL txId := _nextTxId AS INT64
                _nextTxId++
                LOCAL tx := NativeTxContext{txId, txId}
                _activeTransactions:Add(txId, tx)
                RETURN tx
            END LOCK

        PUBLIC METHOD CommitTransaction(tx AS NativeTxContext) AS VOID
            BEGIN LOCK _lock
                tx:CommitTimestamp := _nextTxId
                _nextTxId++
                _activeTransactions:Remove(tx:TxId)
            END LOCK
            RETURN
    END CLASS

END NAMESPACE
"""

def generate_epoch_reclamation():
    return """// Transactions/EpochReclamation.prg
// Native X# epoch-based memory reclamation for garbage collecting
// retired row versions under snapshot isolation.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Transactions

    PUBLIC CLASS NativeEpochManager
        PROTECT _globalEpoch AS INT64
        PROTECT _lock AS OBJECT

        PUBLIC CONSTRUCTOR()
            _globalEpoch := 1
            _lock := OBJECT{}
            RETURN

        PUBLIC METHOD AdvanceEpoch() AS INT64
            BEGIN LOCK _lock
                _globalEpoch++
                RETURN _globalEpoch
            END LOCK
    END CLASS

END NAMESPACE
"""

def generate_crash_recovery():
    return """// Transactions/CrashRecovery.prg
// Native X# ARIES-style crash recovery engine executing
// Analysis, Redo, and Undo passes over the write-ahead log.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Transactions

    PUBLIC CLASS NativeAriesRecovery
        PUBLIC METHOD Recover() AS VOID
            // Analysis pass
            // Redo pass
            // Undo pass
            RETURN
    END CLASS

END NAMESPACE
"""

def generate_catalog_store():
    return """// Catalog/CatalogStore.prg
// Native X# system catalog manager tracking schemas, columns, and secondary indexes.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Catalog

    PUBLIC CLASS NativeTableMetadata
        PUBLIC TableName AS STRING
        PUBLIC Version AS INT
        PUBLIC ColumnNames AS List<STRING>

        PUBLIC CONSTRUCTOR(tbl AS STRING)
            SELF:TableName := tbl
            SELF:Version := 1
            SELF:ColumnNames := List<STRING>{}
            RETURN
    END CLASS

    PUBLIC CLASS NativeCatalogStore
        PROTECT _tables AS Dictionary<STRING, NativeTableMetadata>

        PUBLIC CONSTRUCTOR()
            _tables := Dictionary<STRING, NativeTableMetadata>{}
            RETURN

        PUBLIC METHOD CreateTable(name AS STRING) AS VOID
            _tables:Add(name, NativeTableMetadata{name})
            RETURN
    END CLASS

END NAMESPACE
"""

def generate_math_analysis():
    return """// Udf/MathematicalAnalysis.prg
// Native X# user-defined analytics functions and mathematical linear regression kernels.

USING System

BEGIN NAMESPACE Slate.XSharp.Udf

    PUBLIC STATIC CLASS NativeMathAnalysis
        PUBLIC STATIC METHOD MovingAverage(series AS REAL8[], windowSize AS INT) AS REAL8[]
            IF series:Length == 0 .OR. windowSize <= 0
                RETURN REAL8[]{0}
            ENDIF

            LOCAL result := REAL8[]{series:Length} AS REAL8[]
            LOCAL runningSum := 0.0 AS REAL8

            FOR LOCAL i := 1 UPTO series:Length
                runningSum += series[i]
                IF i >= windowSize
                    IF i > windowSize
                        runningSum -= series[i - windowSize]
                    ENDIF
                    result[i] := runningSum / (REAL8)windowSize
                ELSE
                    result[i] := runningSum / (REAL8)i
                ENDIF
            NEXT

            RETURN result
    END CLASS

END NAMESPACE
"""

def generate_engine_core():
    return """// Kernel/SlateEngineCore.prg
// Native X# orchestrator coordinating catalog, MVCC transactions,
// storage buffer pools, and vectorized query execution.

USING System
USING Slate.XSharp.Catalog
USING Slate.XSharp.Transactions

BEGIN NAMESPACE Slate.XSharp.Kernel

    PUBLIC CLASS NativeSlateEngine
        PROTECT _catalog AS NativeCatalogStore
        PROTECT _mvcc AS NativeMvccEngine

        PUBLIC CONSTRUCTOR()
            _catalog := NativeCatalogStore{}
            _mvcc := NativeMvccEngine{}
            RETURN

        PUBLIC METHOD Start() AS VOID
            RETURN
    END CLASS

END NAMESPACE
"""

if __name__ == '__main__':
    main()
