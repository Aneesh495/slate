// Query/ParserEngine.prg
// Native X# recursive descent parser for SQL and vector constructs.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Query

    PUBLIC CLASS NativeXqlParser
        PROTECT _tokens AS List<NativeToken>
        PROTECT _cursor AS INT

        PUBLIC CONSTRUCTOR(tokens AS List<NativeToken>)
            _tokens := tokens
            _cursor := 0
            RETURN

        PROTECT METHOD Current() AS NativeToken
            RETURN (_cursor < _tokens:Count) ? _tokens[_cursor] : _tokens[_tokens:Count - 1]

        PROTECT METHOD Match(val AS STRING) AS LOGIC
            IF Current():Value:Equals(val, StringComparison.OrdinalIgnoreCase)
                _cursor++
                RETURN TRUE
            ENDIF
            RETURN FALSE

        PUBLIC METHOD ParseStatement() AS NativeStatement
            IF Match("SELECT")
                RETURN ParseSelect()
            ELSEIF Match("CREATE")
                IF Match("TABLE")
                    RETURN ParseCreateTable()
                ENDIF
            ENDIF
            THROW Exception{"Unexpected token: " + Current():Value}

        PROTECT METHOD ParseSelect() AS NativeSelectStatement
            LOCAL stmt := NativeSelectStatement{} AS NativeSelectStatement
            // Parse columns
            WHILE _cursor < _tokens:Count .AND. .NOT. Current():Value:Equals("FROM", StringComparison.OrdinalIgnoreCase)
                stmt:Projections:Add(Current():Value)
                _cursor++
                IF Match(","); LOOP; ENDIF
            ENDDO

            IF Match("FROM")
                stmt:FromTable := Current():Value
                _cursor++
            ENDIF

            RETURN stmt

        PROTECT METHOD ParseCreateTable() AS NativeCreateTableStatement
            LOCAL tblName := Current():Value AS STRING
            _cursor++
            LOCAL stmt := NativeCreateTableStatement{tblName}
            RETURN stmt
    END CLASS

END NAMESPACE
