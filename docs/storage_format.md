# Slate Binary Storage Format Specification

## 1. 64-Byte Aligned BlockHeader

Every physical block on disk or in memory begins with an immutable, cache-line aligned 64-byte `BlockHeader`. This alignment matches modern x86_64 and ARM64 cache line sizes, preventing false sharing across CPU cores and enabling direct DMA transfers.

### Struct Layout (Little-Endian)

| Offset (Bytes) | Field Name | Data Type | Description |
|---|---|---|---|
| 0x00 - 0x03 | `Magic` | `uint32` | Constant magic identifier `0x534C5431` ("SLT1") |
| 0x04 - 0x05 | `Version` | `uint16` | Storage format revision (current: `1`) |
| 0x06 - 0x06 | `BlockType` | `uint8` | Block type classifier (Data, Index, WalFrame, Metadata) |
| 0x07 - 0x07 | `CompressionCodec` | `uint8` | Codec (0: None, 1: RLE, 2: BitPacked, 3: Dictionary, 4: Gorilla) |
| 0x08 - 0x0B | `UncompressedBytes` | `uint32` | Byte length of raw uncompressed payload |
| 0x0C - 0x0F | `CompressedBytes` | `uint32` | Byte length of compressed physical payload |
| 0x10 - 0x13 | `RowCount` | `uint32` | Logical number of records stored in this block |
| 0x14 - 0x17 | `ChecksumCrc32C` | `uint32` | Castagnoli CRC32C checksum of compressed payload |
| 0x18 - 0x1F | `Lsn` | `uint64` | Log Sequence Number associated with this block |
| 0x20 - 0x27 | `MinTimestamp` | `uint64` | Minimum MVCC timestamp present in block |
| 0x28 - 0x3F | `Padding` | `byte[24]` | Reserved zero padding ensuring exact 64-byte struct size |

```
 0                   1                   2                   3
 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                       Magic (0x534C5431)                      |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|          Version              |   BlockType   |  Compression  |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                       UncompressedBytes                       |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                        CompressedBytes                        |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                           RowCount                            |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                        ChecksumCrc32C                         |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                              Lsn                              |
|                                                               |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                         MinTimestamp                          |
|                                                               |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
|                       Reserved Padding                        |
|                          (24 Bytes)                           |
+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
```

---

## 2. Columnar Compression Codecs

Slate selects compression codecs dynamically per column chunk based on data type, cardinality, and distribution:

1. **Run-Length Encoding (RLE)**:
   - Encodes runs of identical contiguous values as `(Value, RunLength)` pairs.
   - Ideal for low-cardinality sorted columns, categorical flags, and status codes.
2. **Frame-of-Reference (FoR) Bit-Packing**:
   - Computes base minimum value $V_{min}$ and maximum delta $\Delta_{max} = V_{max} - V_{min}$.
   - Packs deltas using the minimal necessary bit-width $B = \lceil \log_2(\Delta_{max} + 1) \rceil$.
   - Bit streams are packed into 64-bit unsigned machine words with boundary overflow tracking.
3. **Dictionary Encoding**:
   - Replaces variable-length strings with 8-bit, 16-bit, or 32-bit integer surrogate keys.
   - Includes sorted dictionary header for direct binary search operations on encoded data.
4. **Gorilla Floating-Point XOR Compression**:
   - Compresses IEEE 754 float32 and float64 series via XOR delta coding.
   - Identical successive values compress to a single '0' bit.
   - Non-zero XOR deltas encode leading zero counts, trailing zero counts, and variable-length meaningful bits.

---

## 3. Write-Ahead Log (WAL) Double-Framing

WAL records utilize double-length framing to detect torn disk writes caused by abrupt power failure or OS panics:

```
[4 Bytes: Total Length] [64 Bytes: BlockHeader] [Payload] [4 Bytes: Total Length]
```

During crash recovery, the forward reader verifies:
1. Leading `TotalLength` matches trailing `TotalLength`.
2. `BlockHeader` has valid magic `0x534C5431`.
3. Castagnoli CRC32C matches payload contents.
If any check fails, the recovery reader cleanly halts at the torn boundary.
