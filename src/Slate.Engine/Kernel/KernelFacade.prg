// Kernel/KernelFacade.prg
// Database engine statement classification and routing in native X#

USING System

BEGIN NAMESPACE Slate.Engine.Kernel

    /// <summary>
    /// Database query routing and transaction classification implemented in native X#.
    /// </summary>
    PUBLIC CLASS KernelRouter
        PUBLIC STATIC METHOD IsQueryReadOnly(sql AS STRING) AS LOGIC
            IF String.IsNullOrWhiteSpace(sql)
                RETURN TRUE
            ENDIF

            VAR trimmed := sql:TrimStart():ToUpperInvariant()
            IF trimmed:StartsWith("SELECT") .OR. trimmed:StartsWith("EXPLAIN")
                RETURN TRUE
            ENDIF

            RETURN FALSE

        PUBLIC STATIC METHOD RequiresWriteAheadLog(sql AS STRING) AS LOGIC
            IF String.IsNullOrWhiteSpace(sql)
                RETURN FALSE
            ENDIF

            VAR trimmed := sql:TrimStart():ToUpperInvariant()
            IF trimmed:StartsWith("INSERT") .OR. trimmed:StartsWith("UPDATE") .OR. trimmed:StartsWith("DELETE")
                RETURN TRUE
            ENDIF

            RETURN FALSE
    END CLASS

END NAMESPACE
