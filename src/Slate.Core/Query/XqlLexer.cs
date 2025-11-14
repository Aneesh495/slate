using System.Globalization;
using System.Text;

namespace Slate.Core.Query;

public enum TokenType
{
    // Literals and identifiers
    Identifier,
    IntegerLiteral,
    FloatLiteral,
    StringLiteral,
    VectorLiteral,

    // Keywords
    Select,
    From,
    Where,
    Insert,
    Into,
    Values,
    Delete,
    Update,
    Set,
    Create,
    Table,
    Index,
    Using,
    Drop,
    Begin,
    Commit,
    Abort,
    Transaction,
    As,
    Join,
    On,
    Inner,
    Left,
    Right,
    Cross,
    Group,
    By,
    Order,
    Asc,
    Desc,
    Limit,
    And,
    Or,
    Not,
    Is,
    Null,
    True,
    False,
    Primary,
    Key,
    Explain,
    With,

    // Operators
    Equal,              // =
    NotEqual,           // != or <>
    LessThan,           // <
    LessThanOrEqual,    // <=
    GreaterThan,        // >
    GreaterThanOrEqual, // >=
    Plus,               // +
    Minus,              // -
    Asterisk,           // *
    Slash,              // /
    L2Distance,         // <->
    DotProduct,         // <#>
    CosineDistance,     // <=>

    // Punctuation
    OpenParen,          // (
    CloseParen,         // )
    OpenBracket,        // [
    CloseBracket,       // ]
    Comma,              // ,
    Semicolon,          // ;
    Dot,                // .

    // End of file
    Eof,
    Invalid
}

public sealed record Token(
    TokenType Type,
    string Text,
    int Line,
    int Column,
    long IntValue = 0,
    double FloatValue = 0.0,
    float[]? VectorValue = null);

/// <summary>
/// Lexical scanner for the Extended Query Language (XQL) supporting SQL semantics,
/// multi-dimensional vector literals, and SIMD distance operator tokens.
/// </summary>
public sealed class XqlLexer
{
    private static readonly Dictionary<string, TokenType> Keywords = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SELECT"] = TokenType.Select,
        ["FROM"] = TokenType.From,
        ["WHERE"] = TokenType.Where,
        ["INSERT"] = TokenType.Insert,
        ["INTO"] = TokenType.Into,
        ["VALUES"] = TokenType.Values,
        ["DELETE"] = TokenType.Delete,
        ["UPDATE"] = TokenType.Update,
        ["SET"] = TokenType.Set,
        ["CREATE"] = TokenType.Create,
        ["TABLE"] = TokenType.Table,
        ["INDEX"] = TokenType.Index,
        ["USING"] = TokenType.Using,
        ["DROP"] = TokenType.Drop,
        ["BEGIN"] = TokenType.Begin,
        ["COMMIT"] = TokenType.Commit,
        ["ABORT"] = TokenType.Abort,
        ["TRANSACTION"] = TokenType.Transaction,
        ["AS"] = TokenType.As,
        ["JOIN"] = TokenType.Join,
        ["ON"] = TokenType.On,
        ["INNER"] = TokenType.Inner,
        ["LEFT"] = TokenType.Left,
        ["RIGHT"] = TokenType.Right,
        ["CROSS"] = TokenType.Cross,
        ["GROUP"] = TokenType.Group,
        ["BY"] = TokenType.By,
        ["ORDER"] = TokenType.Order,
        ["ASC"] = TokenType.Asc,
        ["DESC"] = TokenType.Desc,
        ["LIMIT"] = TokenType.Limit,
        ["AND"] = TokenType.And,
        ["OR"] = TokenType.Or,
        ["NOT"] = TokenType.Not,
        ["IS"] = TokenType.Is,
        ["NULL"] = TokenType.Null,
        ["TRUE"] = TokenType.True,
        ["FALSE"] = TokenType.False,
        ["PRIMARY"] = TokenType.Primary,
        ["KEY"] = TokenType.Key,
        ["EXPLAIN"] = TokenType.Explain,
        ["WITH"] = TokenType.With
    };

    private readonly string _source;
    private int _pos;
    private int _line = 1;
    private int _col = 1;

    public XqlLexer(string source)
    {
        _source = source ?? string.Empty;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();
        while (true)
        {
            var token = NextToken();
            tokens.Add(token);
            if (token.Type == TokenType.Eof)
                break;
        }
        return tokens;
    }

    public Token NextToken()
    {
        SkipWhitespaceAndComments();

        if (_pos >= _source.Length)
            return new Token(TokenType.Eof, string.Empty, _line, _col);

        int startLine = _line;
        int startCol = _col;
        char c = _source[_pos];

        // 1. Vector operators & relational operators starting with '<'
        if (c == '<')
        {
            if (Match("<->")) return new Token(TokenType.L2Distance, "<->", startLine, startCol);
            if (Match("<#>")) return new Token(TokenType.DotProduct, "<#>", startLine, startCol);
            if (Match("<=>")) return new Token(TokenType.CosineDistance, "<=>", startLine, startCol);
            if (Match("<=")) return new Token(TokenType.LessThanOrEqual, "<=", startLine, startCol);
            if (Match("<>")) return new Token(TokenType.NotEqual, "<>", startLine, startCol);
            Advance();
            return new Token(TokenType.LessThan, "<", startLine, startCol);
        }

        // 2. Multi-char operators
        if (c == '!' && Peek(1) == '=')
        {
            Advance();
            Advance();
            return new Token(TokenType.NotEqual, "!=", startLine, startCol);
        }
        if (c == '>' && Peek(1) == '=')
        {
            Advance();
            Advance();
            return new Token(TokenType.GreaterThanOrEqual, ">=", startLine, startCol);
        }

        // 3. Single-char punctuation and operators
        switch (c)
        {
            case '=': Advance(); return new Token(TokenType.Equal, "=", startLine, startCol);
            case '>': Advance(); return new Token(TokenType.GreaterThan, ">", startLine, startCol);
            case '+': Advance(); return new Token(TokenType.Plus, "+", startLine, startCol);
            case '-':
                // Check if this is a negative number
                if (char.IsDigit(Peek(1)))
                    return ScanNumber();
                Advance();
                return new Token(TokenType.Minus, "-", startLine, startCol);
            case '*': Advance(); return new Token(TokenType.Asterisk, "*", startLine, startCol);
            case '/': Advance(); return new Token(TokenType.Slash, "/", startLine, startCol);
            case '(': Advance(); return new Token(TokenType.OpenParen, "(", startLine, startCol);
            case ')': Advance(); return new Token(TokenType.CloseParen, ")", startLine, startCol);
            case '[': return ScanVectorOrBracket();
            case ']': Advance(); return new Token(TokenType.CloseBracket, "]", startLine, startCol);
            case ',': Advance(); return new Token(TokenType.Comma, ",", startLine, startCol);
            case ';': Advance(); return new Token(TokenType.Semicolon, ";", startLine, startCol);
            case '.': Advance(); return new Token(TokenType.Dot, ".", startLine, startCol);
        }

        // 4. String literal
        if (c == '\'' || c == '"')
            return ScanString(c);

        // 5. Numeric literal
        if (char.IsDigit(c))
            return ScanNumber();

        // 6. Identifier or Keyword
        if (char.IsLetter(c) || c == '_')
            return ScanIdentifier();

        // Unknown character
        Advance();
        return new Token(TokenType.Invalid, c.ToString(), startLine, startCol);
    }

    private Token ScanVectorOrBracket()
    {
        int startLine = _line;
        int startCol = _col;
        int savedPos = _pos;
        int savedLine = _line;
        int savedCol = _col;

        Advance(); // consume '['

        // Try to parse as vector literal: comma-separated floats ending in ']'
        var floats = new List<float>();
        bool isVector = true;

        while (_pos < _source.Length)
        {
            SkipWhitespaceAndComments();
            if (_pos >= _source.Length)
            {
                isVector = false;
                break;
            }

            if (_source[_pos] == ']')
            {
                Advance();
                break;
            }

            int numStart = _pos;
            if (_source[_pos] == '-' || _source[_pos] == '+')
                Advance();

            bool hasDigits = false;
            while (_pos < _source.Length && (char.IsDigit(_source[_pos]) || _source[_pos] == '.' || _source[_pos] == 'e' || _source[_pos] == 'E' || _source[_pos] == '+' || _source[_pos] == '-'))
            {
                if (char.IsDigit(_source[_pos])) hasDigits = true;
                Advance();
            }

            if (!hasDigits)
            {
                isVector = false;
                break;
            }

            string numStr = _source.Substring(numStart, _pos - numStart);
            if (float.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float val))
            {
                floats.Add(val);
            }
            else
            {
                isVector = false;
                break;
            }

            SkipWhitespaceAndComments();
            if (_pos < _source.Length && _source[_pos] == ',')
            {
                Advance(); // consume ','
            }
            else if (_pos < _source.Length && _source[_pos] == ']')
            {
                Advance(); // consume ']'
                break;
            }
            else
            {
                isVector = false;
                break;
            }
        }

        if (isVector && floats.Count > 0)
        {
            string raw = _source.Substring(savedPos, _pos - savedPos);
            return new Token(TokenType.VectorLiteral, raw, startLine, startCol, VectorValue: floats.ToArray());
        }

        // Fallback: reset and return OpenBracket token
        _pos = savedPos;
        _line = savedLine;
        _col = savedCol;
        Advance();
        return new Token(TokenType.OpenBracket, "[", startLine, startCol);
    }

    private Token ScanString(char quote)
    {
        int startLine = _line;
        int startCol = _col;
        Advance(); // consume open quote

        var sb = new StringBuilder();
        while (_pos < _source.Length)
        {
            char ch = _source[_pos];
            if (ch == '\\' && _pos + 1 < _source.Length)
            {
                Advance();
                char esc = _source[_pos];
                sb.Append(esc switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    _ => esc
                });
                Advance();
            }
            else if (ch == quote)
            {
                Advance(); // consume closing quote
                return new Token(TokenType.StringLiteral, sb.ToString(), startLine, startCol);
            }
            else
            {
                sb.Append(ch);
                Advance();
            }
        }

        return new Token(TokenType.Invalid, sb.ToString(), startLine, startCol);
    }

    private Token ScanNumber()
    {
        int startLine = _line;
        int startCol = _col;
        int startPos = _pos;
        bool isFloat = false;

        if (_source[_pos] == '-' || _source[_pos] == '+')
            Advance();

        while (_pos < _source.Length && char.IsDigit(_source[_pos]))
            Advance();

        if (_pos < _source.Length && _source[_pos] == '.' && _pos + 1 < _source.Length && char.IsDigit(_source[_pos + 1]))
        {
            isFloat = true;
            Advance(); // consume '.'
            while (_pos < _source.Length && char.IsDigit(_source[_pos]))
                Advance();
        }

        if (_pos < _source.Length && (_source[_pos] == 'e' || _source[_pos] == 'E'))
        {
            isFloat = true;
            Advance();
            if (_pos < _source.Length && (_source[_pos] == '+' || _source[_pos] == '-'))
                Advance();
            while (_pos < _source.Length && char.IsDigit(_source[_pos]))
                Advance();
        }

        string text = _source.Substring(startPos, _pos - startPos);
        if (isFloat)
        {
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double fVal);
            return new Token(TokenType.FloatLiteral, text, startLine, startCol, FloatValue: fVal);
        }
        else
        {
            long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long iVal);
            return new Token(TokenType.IntegerLiteral, text, startLine, startCol, IntValue: iVal);
        }
    }

    private Token ScanIdentifier()
    {
        int startLine = _line;
        int startCol = _col;
        int startPos = _pos;

        while (_pos < _source.Length && (char.IsLetterOrDigit(_source[_pos]) || _source[_pos] == '_'))
            Advance();

        string text = _source.Substring(startPos, _pos - startPos);
        if (Keywords.TryGetValue(text, out var kwType))
        {
            return new Token(kwType, text, startLine, startCol);
        }

        return new Token(TokenType.Identifier, text, startLine, startCol);
    }

    private void SkipWhitespaceAndComments()
    {
        while (_pos < _source.Length)
        {
            char c = _source[_pos];
            if (char.IsWhiteSpace(c))
            {
                Advance();
            }
            else if (c == '-' && Peek(1) == '-')
            {
                // Single line SQL comment --
                while (_pos < _source.Length && _source[_pos] != '\n')
                    Advance();
            }
            else if (c == '/' && Peek(1) == '*')
            {
                // Multi-line comment /* ... */
                Advance();
                Advance();
                while (_pos < _source.Length && !(_source[_pos] == '*' && Peek(1) == '/'))
                    Advance();
                if (_pos < _source.Length)
                {
                    Advance();
                    Advance();
                }
            }
            else
            {
                break;
            }
        }
    }

    private bool Match(string prefix)
    {
        if (_pos + prefix.Length > _source.Length)
            return false;

        for (int i = 0; i < prefix.Length; i++)
        {
            if (_source[_pos + i] != prefix[i])
                return false;
        }

        for (int i = 0; i < prefix.Length; i++)
            Advance();

        return true;
    }

    private char Peek(int offset)
    {
        int target = _pos + offset;
        return target < _source.Length ? _source[target] : '\0';
    }

    private void Advance()
    {
        if (_pos < _source.Length)
        {
            if (_source[_pos] == '\n')
            {
                _line++;
                _col = 1;
            }
            else
            {
                _col++;
            }
            _pos++;
        }
    }
}
