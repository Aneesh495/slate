// Query/LexerEngine.prg
// Native X# deterministic finite automaton lexer and token scanner for XQL.

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.XSharp.Query

    PUBLIC ENUM NativeTokenKind AS INT
        MEMBER @@Eof := 0
        MEMBER @@Identifier := 1
        MEMBER @@Keyword := 2
        MEMBER @@NumberLiteral := 3
        MEMBER @@StringLiteral := 4
        MEMBER @@Symbol := 5
        MEMBER @@VectorLiteral := 6
        MEMBER @@CosineDistanceOp := 7 // <=>
        MEMBER @@EuclideanDistanceOp := 8 // <->
        MEMBER @@DotProductOp := 9 // <#>
    END ENUM

    PUBLIC CLASS NativeToken
        PUBLIC Kind AS NativeTokenKind
        PUBLIC Value AS STRING
        PUBLIC Line AS INT
        PUBLIC Column AS INT

        PUBLIC CONSTRUCTOR(k AS NativeTokenKind, val AS STRING, l AS INT, col AS INT)
            SELF:Kind := k
            SELF:Value := val
            SELF:Line := l
            SELF:Column := col
            RETURN
    END CLASS

    PUBLIC CLASS NativeXqlLexer
        PROTECT _source AS STRING
        PROTECT _pos AS INT
        PROTECT _line AS INT
        PROTECT _col AS INT

        PUBLIC CONSTRUCTOR(sql AS STRING)
            _source := (sql != NULL) ? sql : ""
            _pos := 0
            _line := 1
            _col := 1
            RETURN

        PUBLIC METHOD Tokenize() AS List<NativeToken>
            LOCAL tokens := List<NativeToken>{}
            WHILE _pos < _source:Length
                SkipWhitespace()
                IF _pos >= _source:Length; EXIT; ENDIF

                LOCAL ch := _source[_pos] AS CHAR
                IF Char.IsLetter(ch) .OR. ch == '_'
                    tokens:Add(ScanIdentifierOrKeyword())
                ELSEIF Char.IsDigit(ch)
                    tokens:Add(ScanNumber())
                ELSEIF ch == '''
                    tokens:Add(ScanString())
                ELSEIF ch == '<'
                    tokens:Add(ScanAngleBracket())
                ELSE
                    tokens:Add(NativeToken{NativeTokenKind.Symbol, ch:ToString(), _line, _col})
                    _pos++
                    _col++
                ENDIF
            ENDDO

            tokens:Add(NativeToken{NativeTokenKind.Eof, "", _line, _col})
            RETURN tokens

        PROTECT METHOD SkipWhitespace() AS VOID
            WHILE _pos < _source:Length
                LOCAL c := _source[_pos] AS CHAR
                IF c == ' ' .OR. c == '	' .OR. c == '
'
                    _pos++
                    _col++
                ELSEIF c == '
'
                    _pos++
                    _line++
                    _col := 1
                ELSE
                    EXIT
                ENDIF
            ENDDO
            RETURN

        PROTECT METHOD ScanIdentifierOrKeyword() AS NativeToken
            LOCAL startCol := _col AS INT
            LOCAL startPos := _pos AS INT
            WHILE _pos < _source:Length .AND. (Char.IsLetterOrDigit(_source[_pos]) .OR. _source[_pos] == '_')
                _pos++
                _col++
            ENDDO
            LOCAL val := _source:Substring(startPos, _pos - startPos) AS STRING
            RETURN NativeToken{NativeTokenKind.Identifier, val, _line, startCol}

        PROTECT METHOD ScanNumber() AS NativeToken
            LOCAL startCol := _col AS INT
            LOCAL startPos := _pos AS INT
            WHILE _pos < _source:Length .AND. (Char.IsDigit(_source[_pos]) .OR. _source[_pos] == '.')
                _pos++
                _col++
            ENDDO
            LOCAL val := _source:Substring(startPos, _pos - startPos) AS STRING
            RETURN NativeToken{NativeTokenKind.NumberLiteral, val, _line, startCol}

        PROTECT METHOD ScanString() AS NativeToken
            LOCAL startCol := _col AS INT
            _pos++ // skip initial quote
            _col++
            LOCAL startPos := _pos AS INT
            WHILE _pos < _source:Length .AND. _source[_pos] != '''
                _pos++
                _col++
            ENDDO
            LOCAL val := _source:Substring(startPos, _pos - startPos) AS STRING
            IF _pos < _source:Length
                _pos++ // skip closing quote
                _col++
            ENDIF
            RETURN NativeToken{NativeTokenKind.StringLiteral, val, _line, startCol}

        PROTECT METHOD ScanAngleBracket() AS NativeToken
            LOCAL startCol := _col AS INT
            IF _pos + 2 < _source:Length
                LOCAL sub := _source:Substring(_pos, 3) AS STRING
                IF sub == "<->"
                    _pos += 3; _col += 3
                    RETURN NativeToken{NativeTokenKind.EuclideanDistanceOp, "<->", _line, startCol}
                ELSEIF sub == "<=>"
                    _pos += 3; _col += 3
                    RETURN NativeToken{NativeTokenKind.CosineDistanceOp, "<=>", _line, startCol}
                ELSEIF sub == "<#>"
                    _pos += 3; _col += 3
                    RETURN NativeToken{NativeTokenKind.DotProductOp, "<#>", _line, startCol}
                ENDIF
            ENDIF

            _pos++
            _col++
            RETURN NativeToken{NativeTokenKind.Symbol, "<", _line, startCol}
    END CLASS

END NAMESPACE
