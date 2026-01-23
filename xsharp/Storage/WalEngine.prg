// Storage/WalEngine.prg
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
