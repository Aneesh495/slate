// Indexing/BPlusTreeEngine.prg
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
