// Query/XqlParser.prg
// Extended Query Language (XQL) precedence and syntax rules in native X#

USING System

BEGIN NAMESPACE Slate.Core.Query

    /// <summary>
    /// XQL operator precedence and grammar validation in native X#.
    /// </summary>
    PUBLIC CLASS XqlGrammar
        PUBLIC STATIC METHOD GetPrecedence(opName AS STRING) AS INT
            IF String.IsNullOrEmpty(opName)
                RETURN 0
            ENDIF

            VAR upper := opName:ToUpperInvariant()
            IF upper == "OR"
                RETURN 10
            ELSEIF upper == "AND"
                RETURN 20
            ELSEIF upper == "=" .OR. upper == "!=" .OR. upper == "<>" .OR. ;
                   upper == "<" .OR. upper == "<=" .OR. upper == ">" .OR. upper == ">=" .OR. ;
                   upper == "<->" .OR. upper == "<#>" .OR. upper == "<=>"
                RETURN 30
            ELSEIF upper == "+" .OR. upper == "-"
                RETURN 40
            ELSEIF upper == "*" .OR. upper == "/"
                RETURN 50
            ENDIF

            RETURN 0

        PUBLIC STATIC METHOD CanPreFilterVectorSearch(hasWherePredicate AS LOGIC, hasVectorOrderBy AS LOGIC) AS LOGIC
            RETURN hasWherePredicate .AND. hasVectorOrderBy
    END CLASS

END NAMESPACE
