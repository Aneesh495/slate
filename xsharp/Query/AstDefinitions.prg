// Query/AstDefinitions.prg
// Native X# Abstract Syntax Tree hierarchy for XQL statements and expressions.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Query

    PUBLIC ABSTRACT CLASS NativeAstNode
    END CLASS

    PUBLIC ABSTRACT CLASS NativeStatement INHERIT NativeAstNode
    END CLASS

    PUBLIC CLASS NativeCreateTableStatement INHERIT NativeStatement
        PUBLIC TableName AS STRING
        PUBLIC Columns AS List<STRING>
        PUBLIC Types AS List<STRING>

        PUBLIC CONSTRUCTOR(tbl AS STRING)
            SELF:TableName := tbl
            SELF:Columns := List<STRING>{}
            SELF:Types := List<STRING>{}
            RETURN
    END CLASS

    PUBLIC CLASS NativeSelectStatement INHERIT NativeStatement
        PUBLIC Projections AS List<STRING>
        PUBLIC FromTable AS STRING
        PUBLIC WhereClause AS STRING
        PUBLIC OrderByClause AS STRING
        PUBLIC LimitCount AS INT

        PUBLIC CONSTRUCTOR()
            SELF:Projections := List<STRING>{}
            SELF:FromTable := ""
            SELF:WhereClause := ""
            SELF:OrderByClause := ""
            SELF:LimitCount := -1
            RETURN
    END CLASS

    PUBLIC CLASS NativeInsertStatement INHERIT NativeStatement
        PUBLIC TableName AS STRING
        PUBLIC Values AS List<STRING>

        PUBLIC CONSTRUCTOR(tbl AS STRING)
            SELF:TableName := tbl
            SELF:Values := List<STRING>{}
            RETURN
    END CLASS

END NAMESPACE
