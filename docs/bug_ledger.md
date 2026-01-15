# Slate Defect and Regression Bug Ledger

This document tracks all defects, architectural oversights, and race conditions identified during the development of the Slate database engine. In accordance with zero-defect engineering standards, every bug is documented with its root cause analysis, reproduction test case, and permanent fix.

---

### Bug 001: BlockHeader 64-Byte Cache Alignment Padding Miscalculation

- **Component**: `Slate.Core.Primitives.BlockHeader`
- **Severity**: High (Memory Alignment Violation)
- **Symptom**: `Marshal.SizeOf<BlockHeader>()` evaluated to 68 bytes instead of the required 64 bytes, breaking CPU L1/L2 cache line alignment.
- **Root Cause**: The header defines 40 bytes of explicit fields (Magic, Version, Type, Codec, UncompressedBytes, CompressedBytes, RowCount, Checksum, Lsn, MinTimestamp). The reserved padding was initially set to 28 bytes ($40 + 28 = 68$) instead of 24 bytes ($40 + 24 = 64$).
- **Reproduction Test**: `tests/Slate.Tests/BlockHeaderTests.cs` -> `BlockHeader_StructSize_IsExactly64Bytes`
- **Resolution**: Adjusted `_padding` fixed buffer in `BlockHeader` from 28 bytes to 24 bytes. Added unit test assertion enforcing `Marshal.SizeOf<BlockHeader>() == 64`.

---

### Bug 002: BitPackedCodec Word Boundary Bit-Shift Exhaustion

- **Component**: `Slate.Core.Storage.BitPackedCodec`
- **Severity**: Critical (Data Corruption)
- **Symptom**: During 64-bit integer bit unpacking, when `bitsAvailable == 0`, values near word boundaries were read as zero or corrupt high bits.
- **Root Cause**: In C#, shifting a 64-bit integer by 64 bits (`value >> 64`) is a no-op that shifts by `64 % 64 = 0` bits. When `bitsAvailable` reached 0, the decoder attempted a 64-bit shift instead of proactively loading the next word.
- **Reproduction Test**: `tests/Slate.Tests/BitPackedCodecTests.cs` -> `BitPackedCodec_BoundaryWordExhaustion_PreservesIntegrity`
- **Resolution**: Refactored the bit-unpacking loop to check `if (bitsAvailable == 0)` and eagerly fetch the next 64-bit word before calculating shift offsets.

---

### Bug 003: WalWriter BinaryWriter Premature Stream Disposal

- **Component**: `Slate.Core.Storage.WalWriter`
- **Severity**: Medium (Resource Premature Closure)
- **Symptom**: When `WalWriter` was disposed or flushed, attempting to read the underlying `MemoryStream` threw `ObjectDisposedException`.
- **Root Cause**: The default constructor for `BinaryWriter(stream, encoding)` assumes ownership and closes the base stream upon disposal.
- **Reproduction Test**: `tests/Slate.Tests/WalTests.cs` -> `WalWriter_Flush_DoesNotCloseUnderlyingStream`
- **Resolution**: Passed `leaveOpen: true` to the `BinaryWriter` constructor in `WalWriter`.

---

### Bug 004: MemTable ReaderWriterLockSlim Recursion Exception

- **Component**: `Slate.Core.Storage.MemTable`
- **Severity**: High (Thread Lock Exception)
- **Symptom**: Calling `FlushToVectorBatch()` raised `LockRecursionException` when scanning entries.
- **Root Cause**: `FlushToVectorBatch()` held a read lock and subsequently invoked `Get()`, which also attempted to acquire a read lock on the same thread without recursive lock policy enabled.
- **Reproduction Test**: `tests/Slate.Tests/MemTableTests.cs` -> `MemTable_FlushToVectorBatch_DoesNotThrowLockRecursion`
- **Resolution**: Separated lock-free internal retrieval helper `GetNoLock()` from public locked accessor `Get()`, and configured `ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion)`.

---

### Bug 005: AdaptiveRadixTree Node Child Slot Overwrite Anomaly

- **Component**: `Slate.Core.Indexing.AdaptiveRadixTree`
- **Severity**: Medium (Key Duplication)
- **Symptom**: Updating an existing key in an ART node resulted in duplicate child keys instead of in-place pointer replacement.
- **Root Cause**: Node insertion methods (`AddChild`) appended the child without first checking if the child key byte already existed in the node's key array.
- **Reproduction Test**: `tests/Slate.Tests/AdaptiveRadixTreeTests.cs` -> `AdaptiveRadixTree_UpdateExistingKey_DoesNotDuplicate`
- **Resolution**: Added `AddOrUpdateChild` method to all ART node variants (`Node4`, `Node16`, `Node48`, `Node256`) that replaces the pointer when the key byte is already present.

---

### Bug 006: MVCC Uncommitted Version Visibility Leak

- **Component**: `Slate.Core.Transactions.MvccManager`
- **Severity**: High (Isolation Anomaly)
- **Symptom**: An active transaction's written rows were observable by concurrent transactions prior to `CommitTransaction()` being called.
- **Root Cause**: `RecordVersion()` stamped `CreatedTimestamp` with `tx.ReadTimestamp` rather than `0` (uncommitted), causing snapshot visibility checks (`v.CreatedTimestamp <= reader.ReadTimestamp`) to evaluate to true before commit.
- **Reproduction Test**: `tests/Slate.Tests/MvccTests.cs` -> `MvccManager_VersionStore_RespectsVisibility`
- **Resolution**: Set `CreatedTimestamp = 0` for all active transactional writes. Updated `CommitTransaction()` to update `CreatedTimestamp = commitTimestamp` for all keys in the write-set upon successful commit.
