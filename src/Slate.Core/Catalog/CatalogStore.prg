// Catalog/CatalogStore.prg
// Table catalog naming and validation rules in native X#

USING System

BEGIN NAMESPACE Slate.Core.Catalog

    /// <summary>
    /// Catalog validation rules implemented in native X#.
    /// </summary>
    PUBLIC CLASS CatalogValidator
        PUBLIC STATIC METHOD IsValidIdentifier(name AS STRING) AS LOGIC
            IF String.IsNullOrWhiteSpace(name)
                RETURN FALSE
            ENDIF

            // Identifier must start with letter or underscore
            VAR firstChar := name[0]
            IF .NOT. (Char.IsLetter(firstChar) .OR. firstChar == '_')
                RETURN FALSE
            ENDIF

            // Remaining characters must be alphanumeric or underscore
            FOR VAR i := 1 TO name:Length - 1
                VAR ch := name[i]
                IF .NOT. (Char.IsLetterOrDigit(ch) .OR. ch == '_')
                    RETURN FALSE
                ENDIF
            NEXT

            RETURN TRUE

        PUBLIC STATIC METHOD IsValidSchemaEvolution(oldVersion AS UINT32, newVersion AS UINT32) AS LOGIC
            RETURN newVersion > oldVersion
    END CLASS

END NAMESPACE
