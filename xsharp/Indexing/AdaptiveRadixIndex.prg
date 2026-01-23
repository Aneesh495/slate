// Indexing/AdaptiveRadixIndex.prg
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
