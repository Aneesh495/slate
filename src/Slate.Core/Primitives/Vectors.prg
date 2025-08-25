// Primitives/Vectors.prg
// Column vector manipulation and selection masks in native X#

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.Core.Primitives

    /// <summary>
    /// Vector projection, slicing, and selection filtering in native X#.
    /// </summary>
    PUBLIC CLASS VectorOperations
        PUBLIC STATIC METHOD FilterInt64(vec AS ColumnVector, threshold AS INT64, op AS STRING) AS INT[]
            LOCAL matches := List<INT>{} AS List<INT>
            LOCAL len := vec:Length AS INT
            LOCAL r AS INT

            FOR r := 0 UPTO len - 1
                IF vec:IsNull(r)
                    LOOP
                ENDIF

                LOCAL val := vec:GetInt64(r) AS INT64
                LOCAL passed := FALSE AS LOGIC

                DO CASE
                CASE op == "=" .OR. op == "=="
                    passed := (val == threshold)
                CASE op == ">"
                    passed := (val > threshold)
                CASE op == ">="
                    passed := (val >= threshold)
                CASE op == "<"
                    passed := (val < threshold)
                CASE op == "<="
                    passed := (val <= threshold)
                CASE op == "!=" .OR. op == "<>"
                    passed := (val != threshold)
                ENDCASE

                IF passed
                    matches:Add(r)
                ENDIF
            NEXT

            RETURN matches:ToArray()

        PUBLIC STATIC METHOD FilterDouble(vec AS ColumnVector, threshold AS REAL8, op AS STRING) AS INT[]
            LOCAL matches := List<INT>{} AS List<INT>
            LOCAL len := vec:Length AS INT
            LOCAL r AS INT

            FOR r := 0 UPTO len - 1
                IF vec:IsNull(r)
                    LOOP
                ENDIF

                LOCAL val := vec:GetDouble(r) AS REAL8
                LOCAL passed := FALSE AS LOGIC

                DO CASE
                CASE op == "=" .OR. op == "=="
                    passed := Math.Abs(val - threshold) < 1e-9
                CASE op == ">"
                    passed := (val > threshold)
                CASE op == ">="
                    passed := (val >= threshold)
                CASE op == "<"
                    passed := (val < threshold)
                CASE op == "<="
                    passed := (val <= threshold)
                CASE op == "!=" .OR. op == "<>"
                    passed := Math.Abs(val - threshold) >= 1e-9
                ENDCASE

                IF passed
                    matches:Add(r)
                ENDIF
            NEXT

            RETURN matches:ToArray()
    END CLASS

END NAMESPACE
