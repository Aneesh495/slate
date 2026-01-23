// Storage/ZoneMaps.prg
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
