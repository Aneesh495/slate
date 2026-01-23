// Indexing/RoaringBitmapIndex.prg
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
