// ReplPrompt.prg
// REPL prompt formatting and dot-command parsing in native X#

USING System

BEGIN NAMESPACE Slate.Cli

    /// <summary>
    /// REPL input line classifier implemented in native X#.
    /// </summary>
    PUBLIC CLASS ReplInputClassifier
        PUBLIC STATIC METHOD IsDotCommand(line AS STRING) AS LOGIC
            IF String.IsNullOrWhiteSpace(line)
                RETURN FALSE
            ENDIF
            RETURN line:TrimStart():StartsWith(".")

        PUBLIC STATIC METHOD IsCompleteStatement(sql AS STRING) AS LOGIC
            IF String.IsNullOrWhiteSpace(sql)
                RETURN FALSE
            ENDIF
            RETURN sql:TrimEnd():EndsWith(";")
    END CLASS

END NAMESPACE
