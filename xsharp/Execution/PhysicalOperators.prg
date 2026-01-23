// Execution/PhysicalOperators.prg
// Native X# volcano-style physical operator pipeline and vector batch transfer.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Execution

    PUBLIC ABSTRACT CLASS NativePhysicalOperator
        PUBLIC Children AS List<NativePhysicalOperator>

        PROTECT CONSTRUCTOR()
            SELF:Children := List<NativePhysicalOperator>{}
            RETURN

        PUBLIC ABSTRACT METHOD Open() AS VOID
        PUBLIC ABSTRACT METHOD NextBatch() AS LOGIC
        PUBLIC ABSTRACT METHOD Close() AS VOID
    END CLASS

    PUBLIC CLASS NativeTableScanOperator INHERIT NativePhysicalOperator
        PUBLIC TableName AS STRING
        PROTECT _isOpen AS LOGIC

        PUBLIC CONSTRUCTOR(tbl AS STRING)
            SUPER()
            SELF:TableName := tbl
            _isOpen := FALSE
            RETURN

        PUBLIC OVERRIDE METHOD Open() AS VOID
            _isOpen := TRUE
            RETURN

        PUBLIC OVERRIDE METHOD NextBatch() AS LOGIC
            IF .NOT. _isOpen; RETURN FALSE; ENDIF
            RETURN FALSE // End of scan
        
        PUBLIC OVERRIDE METHOD Close() AS VOID
            _isOpen := FALSE
            RETURN
    END CLASS

END NAMESPACE
