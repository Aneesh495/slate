// Query/XqlTypeChecker.prg
// Extended Query Language (XQL) type checking rules in native X#

USING System

BEGIN NAMESPACE Slate.Core.Query

    /// <summary>
    /// Type compatibility and numeric promotion rules in native X#.
    /// </summary>
    PUBLIC CLASS XqlTypePromoter
        PUBLIC STATIC METHOD AreCompatibleNumericTypes(leftType AS BYTE, rightType AS BYTE) AS LOGIC
            // Both must be numeric types
            RETURN leftType <= 6 .AND. rightType <= 6

        PUBLIC STATIC METHOD ValidateVectorDimensions(leftDim AS INT, rightDim AS INT) AS LOGIC
            IF leftDim <= 0 .OR. rightDim <= 0
                RETURN TRUE // Dimension unknown at parse time
            ENDIF
            RETURN leftDim == rightDim
    END CLASS

END NAMESPACE
