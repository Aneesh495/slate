// Indexing/RoaringBitmapIndex.prg
// Inverted index posting lists and multi-attribute bitmap evaluation in native X#

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.Core.Indexing

    /// <summary>
    /// Inverted index operations and posting list set algebra in native X#.
    /// </summary>
    PUBLIC CLASS InvertedIndexManager
        PRIVATE _index := Dictionary<STRING, RoaringBitmap>{StringComparer.Ordinal} AS Dictionary<STRING, RoaringBitmap>

        PUBLIC CONSTRUCTOR()
            SELF:_index := Dictionary<STRING, RoaringBitmap>{StringComparer.Ordinal}
            RETURN

        PUBLIC METHOD IndexTerm(term AS STRING, rowId AS UINT64) AS VOID
            IF String.IsNullOrEmpty(term)
                RETURN
            ENDIF

            LOCAL bitmap AS RoaringBitmap
            IF .NOT. SELF:_index:TryGetValue(term, OUT bitmap)
                bitmap := RoaringBitmap{}
                SELF:_index[term] := bitmap
            ENDIF

            bitmap:Add(rowId)

        PUBLIC METHOD SearchConjunction(terms AS STRING[]) AS UINT64[]
            IF terms == NULL .OR. terms:Length == 0
                RETURN UINT64[]{0}
            ENDIF

            LOCAL result AS RoaringBitmap
            LOCAL first := TRUE AS LOGIC

            LOCAL i AS INT
            FOR i := 0 UPTO terms:Length - 1
                LOCAL term := terms[i] AS STRING
                LOCAL bitmap AS RoaringBitmap
                IF SELF:_index:TryGetValue(term, OUT bitmap)
                    IF first
                        result := bitmap
                        first := FALSE
                    ELSE
                        result := result:And(bitmap)
                    ENDIF
                ELSE
                    // Term not found, conjunction yields empty set
                    RETURN UINT64[]{0}
                ENDIF
            NEXT

            RETURN IIF(result != NULL, result:ToArray(), UINT64[]{0})
    END CLASS

END NAMESPACE
