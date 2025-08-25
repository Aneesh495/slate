// Primitives/Schema.prg
// Table schema manipulation, column hashing, and validation in native X#

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.Core.Primitives

    /// <summary>
    /// Schema validation and schema evolution tools in native X#.
    /// </summary>
    PUBLIC CLASS SchemaValidator
        PUBLIC STATIC METHOD ValidateRow(schema AS TableSchema, values AS SlateValue[]) AS LOGIC
            IF values == NULL .OR. values:Length != schema:ColumnCount
                RETURN FALSE
            ENDIF

            LOCAL i AS INT
            FOR i := 0 UPTO schema:ColumnCount - 1
                LOCAL col := schema[i] AS ColumnDescriptor
                LOCAL val := values[i] AS SlateValue

                IF val:IsNull
                    IF .NOT. col:IsNullable
                        RETURN FALSE
                    ENDIF
                    LOOP
                ENDIF

                IF val:Type != col:Type
                    // Permit safe numeric widening
                    IF .NOT. (val:Type:IsNumeric() .AND. col:Type:IsNumeric())
                        RETURN FALSE
                    ENDIF
                ENDIF

                // Check vector dimensions
                IF col:Type == DataType.VectorFloat32
                    LOCAL vec := val:AsVector() AS REAL4[]
                    IF vec == NULL .OR. vec:Length != col:VectorDimension
                        RETURN FALSE
                    ENDIF
                ENDIF
            NEXT

            RETURN TRUE
    END CLASS

END NAMESPACE
