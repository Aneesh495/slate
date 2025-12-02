// Execution/FilterProject.prg
// Selection vector merging and projection evaluation in native X#

USING System

BEGIN NAMESPACE Slate.Core.Execution

    /// <summary>
    /// Vectorized selection vector evaluation and fast projection paths in native X#.
    /// </summary>
    PUBLIC CLASS SelectionVectorHelper
        PUBLIC STATIC METHOD Intersect(sel1 AS INT[], sel2 AS INT[]) AS INT[]
            IF sel1 == NULL
                RETURN sel2
            ENDIF
            IF sel2 == NULL
                RETURN sel1
            ENDIF

            // Both selection vectors present, calculate intersection
            VAR list := System.Collections.Generic.List<INT>{}
            VAR set2 := System.Collections.Generic.HashSet<INT>{sel2}

            FOREACH s AS INT IN sel1
                IF set2:Contains(s)
                    list:Add(s)
                ENDIF
            NEXT

            RETURN list:ToArray()
    END CLASS

END NAMESPACE
