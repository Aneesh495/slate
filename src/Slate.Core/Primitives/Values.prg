// Primitives/Values.prg
// High performance scalar boxing and value boxing utilities in native X#

USING System
USING System.Text
USING System.Globalization

BEGIN NAMESPACE Slate.Core.Primitives

    /// <summary>
    /// Value boxing and conversion helpers defined in X#.
    /// </summary>
    PUBLIC CLASS SlateValueHelper
        PUBLIC STATIC METHOD FormatValue(val AS SlateValue) AS STRING
            IF val:IsNull
                RETURN "NULL"
            ENDIF

            DO CASE
            CASE val:Type == DataType.Boolean
                RETURN IIF(val:AsBoolean(), "TRUE", "FALSE")
            CASE val:Type == DataType.Int8 .OR. val:Type == DataType.Int16 .OR. val:Type == DataType.Int32 .OR. val:Type == DataType.Int64
                RETURN val:AsInt64():ToString(CultureInfo.InvariantCulture)
            CASE val:Type == DataType.Float32 .OR. val:Type == DataType.Float64
                RETURN val:AsFloat64():ToString("G17", CultureInfo.InvariantCulture)
            CASE val:Type == DataType.String
                RETURN val:AsString()
            CASE val:Type == DataType.TimestampUtc
                RETURN DateTimeOffset.FromUnixTimeMilliseconds(val:AsInt64() / 1000):ToString("o")
            CASE val:Type == DataType.VectorFloat32
                LOCAL vec := val:AsVector() AS REAL4[]
                IF vec == NULL
                    RETURN "[]"
                ENDIF
                LOCAL sb := StringBuilder{"["} AS StringBuilder
                LOCAL i AS INT
                FOR i := 0 UPTO Math.Min(vec:Length, 4) - 1
                    IF i > 0
                        sb:Append(", ")
                    ENDIF
                    sb:Append(vec[i]:ToString("F4", CultureInfo.InvariantCulture))
                NEXT
                IF vec:Length > 4
                    sb:Append(", ... (dim=" + vec:Length:ToString() + ")")
                ENDIF
                sb:Append("]")
                RETURN sb:ToString()
            OTHERWISE
                RETURN val:ToString()
            ENDCASE
    END CLASS

END NAMESPACE
