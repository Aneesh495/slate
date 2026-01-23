// Indexing/HnswVectorIndex.prg
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
