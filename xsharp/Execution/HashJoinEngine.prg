// Execution/HashJoinEngine.prg
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
