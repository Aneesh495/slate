// Udf/UdfCatalog.prg
// Function resolution and type checking rules in native X#

USING System

BEGIN NAMESPACE Slate.Core.Udf

    /// <summary>
    /// UDF parameter matching and signature validation in native X#.
    /// </summary>
    PUBLIC CLASS UdfSignatureValidator
        PUBLIC STATIC METHOD ValidateArgCount(expected AS INT, actual AS INT) AS LOGIC
            RETURN expected == actual

        PUBLIC STATIC METHOD IsBuiltinFunction(name AS STRING) AS LOGIC
            IF String.IsNullOrEmpty(name)
                RETURN FALSE
            ENDIF

            VAR upper := name:ToUpperInvariant()
            RETURN upper == "SIGMOID" .OR. upper == "ENTROPY2" .OR. upper == "L2_NORM"
    END CLASS

END NAMESPACE
