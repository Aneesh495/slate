// Execution/AggregationEngine.prg
// Native X# hash-based grouped and scalar aggregations (SUM, AVG, MIN, MAX, COUNT).

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Execution

    PUBLIC CLASS NativeGroupAccumulator
        PUBLIC Count AS INT64
        PUBLIC SumInt AS INT64
        PUBLIC SumFloat AS REAL8

        PUBLIC CONSTRUCTOR()
            SELF:Count := 0
            SELF:SumInt := 0
            SELF:SumFloat := 0.0
            RETURN

        PUBLIC METHOD AccumulateInt(val AS INT64) AS VOID
            SELF:Count++
            SELF:SumInt += val
            RETURN

        PUBLIC METHOD AccumulateFloat(val AS REAL8) AS VOID
            SELF:Count++
            SELF:SumFloat += val
            RETURN
    END CLASS

    PUBLIC CLASS NativeAggregationEngine
        PROTECT _groups AS Dictionary<STRING, NativeGroupAccumulator>

        PUBLIC CONSTRUCTOR()
            _groups := Dictionary<STRING, NativeGroupAccumulator>{}
            RETURN

        PUBLIC METHOD AddInt(groupKey AS STRING, val AS INT64) AS VOID
            IF .NOT. _groups:ContainsKey(groupKey)
                _groups:Add(groupKey, NativeGroupAccumulator{})
            ENDIF
            _groups[groupKey]:AccumulateInt(val)
            RETURN
    END CLASS

END NAMESPACE
