// Query/OptimizerRules.prg
// Native X# cost-based optimization rules, predicate pushdown,
// and vector KNN pattern recognition for execution plan rewrites.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Query

    PUBLIC CLASS NativeLogicalScan
        PUBLIC TableName AS STRING
        PUBLIC ProjectedColumns AS List<STRING>
        PUBLIC PushdownFilter AS STRING

        PUBLIC CONSTRUCTOR(tbl AS STRING)
            SELF:TableName := tbl
            SELF:ProjectedColumns := List<STRING>{}
            SELF:PushdownFilter := ""
            RETURN
    END CLASS

    PUBLIC STATIC CLASS NativeOptimizerRules
        PUBLIC STATIC METHOD PushdownPredicate(scan AS NativeLogicalScan, filterExpr AS STRING) AS VOID
            scan:PushdownFilter := filterExpr
            RETURN

        PUBLIC STATIC METHOD PruneProjections(scan AS NativeLogicalScan, requiredCols AS HashSet<STRING>) AS VOID
            scan:ProjectedColumns:RemoveAll({col => .NOT. requiredCols:Contains(col)})
            RETURN
    END CLASS

END NAMESPACE
