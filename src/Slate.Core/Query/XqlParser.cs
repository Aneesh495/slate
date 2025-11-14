using System.Globalization;
using Slate.Core.Catalog;
using Slate.Core.Primitives;

namespace Slate.Core.Query;

public sealed class XqlParseException : Exception
{
    public int Line { get; }
    public int Column { get; }

    public XqlParseException(string message, int line, int column)
        : base($"{message} (Line {line}, Column {column})")
    {
        Line = line;
        Column = column;
    }
}

/// <summary>
/// Recursive descent parser for the Extended Query Language (XQL).
/// </summary>
public sealed class XqlParser
{
    private readonly List<Token> _tokens;
    private int _pos;

    public XqlParser(List<Token> tokens)
    {
        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
    }

    public static StatementNode Parse(string sql)
    {
        var lexer = new XqlLexer(sql);
        var tokens = lexer.Tokenize();
        var parser = new XqlParser(tokens);
        return parser.ParseStatement();
    }

    public StatementNode ParseStatement()
    {
        var token = Peek();

        switch (token.Type)
        {
            case TokenType.Select:
                return ParseSelect();
            case TokenType.Insert:
                return ParseInsert();
            case TokenType.Update:
                return ParseUpdate();
            case TokenType.Delete:
                return ParseDelete();
            case TokenType.Create:
                return ParseCreate();
            case TokenType.Drop:
                return ParseDrop();
            case TokenType.Begin:
                return ParseBegin();
            case TokenType.Commit:
                Advance();
                Optional(TokenType.Semicolon);
                return new TransactionStatement(TransactionAction.Commit);
            case TokenType.Abort:
                Advance();
                Optional(TokenType.Semicolon);
                return new TransactionStatement(TransactionAction.Abort);
            case TokenType.Explain:
                Advance();
                var inner = ParseStatement();
                return new ExplainStatement(inner);
            default:
                throw new XqlParseException($"Unexpected statement start token '{token.Text}'.", token.Line, token.Column);
        }
    }

    private SelectStatement ParseSelect()
    {
        Consume(TokenType.Select);

        var projections = new List<SelectItem>();
        if (Match(TokenType.Asterisk))
        {
            projections.Add(new SelectItem(new ColumnReferenceExpression(null, "*")));
        }
        else
        {
            while (true)
            {
                var expr = ParseExpression();
                string? alias = null;
                if (Match(TokenType.As))
                {
                    alias = Consume(TokenType.Identifier).Text;
                }
                else if (Peek().Type == TokenType.Identifier)
                {
                    alias = Advance().Text;
                }

                projections.Add(new SelectItem(expr, alias));

                if (!Match(TokenType.Comma))
                    break;
            }
        }

        string? fromTable = null;
        string? fromAlias = null;
        if (Match(TokenType.From))
        {
            fromTable = Consume(TokenType.Identifier).Text;
            if (Match(TokenType.As))
            {
                fromAlias = Consume(TokenType.Identifier).Text;
            }
            else if (Peek().Type == TokenType.Identifier && Peek().Type != TokenType.Where && Peek().Type != TokenType.Join && Peek().Type != TokenType.Order && Peek().Type != TokenType.Group && Peek().Type != TokenType.Limit)
            {
                fromAlias = Advance().Text;
            }
        }

        var joins = new List<JoinClause>();
        while (true)
        {
            var joinType = JoinType.Inner;
            if (Match(TokenType.Left)) { Consume(TokenType.Join); joinType = JoinType.Left; }
            else if (Match(TokenType.Right)) { Consume(TokenType.Join); joinType = JoinType.Right; }
            else if (Match(TokenType.Cross)) { Consume(TokenType.Join); joinType = JoinType.Cross; }
            else if (Match(TokenType.Inner)) { Consume(TokenType.Join); joinType = JoinType.Inner; }
            else if (Match(TokenType.Join)) { joinType = JoinType.Inner; }
            else break;

            string joinTable = Consume(TokenType.Identifier).Text;
            string? joinAlias = null;
            if (Match(TokenType.As)) joinAlias = Consume(TokenType.Identifier).Text;
            else if (Peek().Type == TokenType.Identifier && Peek().Type != TokenType.On) joinAlias = Advance().Text;

            ExpressionNode? onCond = null;
            if (joinType != JoinType.Cross && Match(TokenType.On))
            {
                onCond = ParseExpression();
            }

            joins.Add(new JoinClause(joinType, joinTable, joinAlias, onCond));
        }

        ExpressionNode? where = null;
        if (Match(TokenType.Where))
        {
            where = ParseExpression();
        }

        List<ExpressionNode>? groupBy = null;
        if (Match(TokenType.Group))
        {
            Consume(TokenType.By);
            groupBy = new List<ExpressionNode>();
            while (true)
            {
                groupBy.Add(ParseExpression());
                if (!Match(TokenType.Comma))
                    break;
            }
        }

        List<OrderByClause>? orderBy = null;
        if (Match(TokenType.Order))
        {
            Consume(TokenType.By);
            orderBy = new List<OrderByClause>();
            while (true)
            {
                var expr = ParseExpression();
                bool asc = true;
                if (Match(TokenType.Desc)) asc = false;
                else Match(TokenType.Asc);

                orderBy.Add(new OrderByClause(expr, asc));
                if (!Match(TokenType.Comma))
                    break;
            }
        }

        int? limit = null;
        if (Match(TokenType.Limit))
        {
            var tok = Consume(TokenType.IntegerLiteral);
            limit = (int)tok.IntValue;
        }

        Optional(TokenType.Semicolon);
        return new SelectStatement(projections, fromTable, fromAlias, joins.Count > 0 ? joins : null, where, groupBy, orderBy, limit);
    }

    private InsertStatement ParseInsert()
    {
        Consume(TokenType.Insert);
        Consume(TokenType.Into);
        string table = Consume(TokenType.Identifier).Text;

        List<string>? columns = null;
        if (Match(TokenType.OpenParen))
        {
            columns = new List<string>();
            while (true)
            {
                columns.Add(Consume(TokenType.Identifier).Text);
                if (!Match(TokenType.Comma))
                    break;
            }
            Consume(TokenType.CloseParen);
        }

        Consume(TokenType.Values);
        var rows = new List<List<ExpressionNode>>();

        while (true)
        {
            Consume(TokenType.OpenParen);
            var row = new List<ExpressionNode>();
            while (true)
            {
                row.Add(ParseExpression());
                if (!Match(TokenType.Comma))
                    break;
            }
            Consume(TokenType.CloseParen);
            rows.Add(row);

            if (!Match(TokenType.Comma))
                break;
        }

        Optional(TokenType.Semicolon);
        return new InsertStatement(table, columns, rows);
    }

    private UpdateStatement ParseUpdate()
    {
        Consume(TokenType.Update);
        string table = Consume(TokenType.Identifier).Text;
        Consume(TokenType.Set);

        var assignments = new List<AssignmentClause>();
        while (true)
        {
            string col = Consume(TokenType.Identifier).Text;
            Consume(TokenType.Equal);
            var val = ParseExpression();
            assignments.Add(new AssignmentClause(col, val));

            if (!Match(TokenType.Comma))
                break;
        }

        ExpressionNode? where = null;
        if (Match(TokenType.Where))
        {
            where = ParseExpression();
        }

        Optional(TokenType.Semicolon);
        return new UpdateStatement(table, assignments, where);
    }

    private DeleteStatement ParseDelete()
    {
        Consume(TokenType.Delete);
        Consume(TokenType.From);
        string table = Consume(TokenType.Identifier).Text;

        ExpressionNode? where = null;
        if (Match(TokenType.Where))
        {
            where = ParseExpression();
        }

        Optional(TokenType.Semicolon);
        return new DeleteStatement(table, where);
    }

    private StatementNode ParseCreate()
    {
        Consume(TokenType.Create);

        if (Match(TokenType.Table))
        {
            string tableName = Consume(TokenType.Identifier).Text;
            Consume(TokenType.OpenParen);

            var columns = new List<ColumnDefinition>();
            string primaryKey = string.Empty;

            while (true)
            {
                if (Match(TokenType.Primary))
                {
                    Consume(TokenType.Key);
                    Consume(TokenType.OpenParen);
                    primaryKey = Consume(TokenType.Identifier).Text;
                    Consume(TokenType.CloseParen);
                }
                else
                {
                    string colName = Consume(TokenType.Identifier).Text;
                    string typeName = Consume(TokenType.Identifier).Text.ToUpperInvariant();

                    DataType type;
                    int dim = 0;

                    if (typeName == "INT" || typeName == "INTEGER" || typeName == "BIGINT" || typeName == "INT64")
                    {
                        type = DataType.Int64;
                    }
                    else if (typeName == "FLOAT" || typeName == "DOUBLE" || typeName == "REAL" || typeName == "FLOAT64")
                    {
                        type = DataType.Float64;
                    }
                    else if (typeName == "FLOAT32")
                    {
                        type = DataType.Float32;
                    }
                    else if (typeName == "BOOLEAN" || typeName == "BOOL")
                    {
                        type = DataType.Boolean;
                    }
                    else if (typeName == "STRING" || typeName == "TEXT" || typeName == "VARCHAR")
                    {
                        type = DataType.String;
                    }
                    else if (typeName == "TIMESTAMP")
                    {
                        type = DataType.TimestampUtc;
                    }
                    else if (typeName == "VECTOR")
                    {
                        type = DataType.VectorFloat32;
                        Consume(TokenType.OpenParen);
                        dim = (int)Consume(TokenType.IntegerLiteral).IntValue;
                        Consume(TokenType.CloseParen);
                    }
                    else
                    {
                        throw new XqlParseException($"Unknown column type '{typeName}'.", Peek().Line, Peek().Column);
                    }

                    bool nullable = true;
                    bool isPk = false;

                    while (true)
                    {
                        if (Match(TokenType.Not))
                        {
                            Consume(TokenType.Null);
                            nullable = false;
                        }
                        else if (Match(TokenType.Primary))
                        {
                            Consume(TokenType.Key);
                            isPk = true;
                            primaryKey = colName;
                        }
                        else
                        {
                            break;
                        }
                    }

                    columns.Add(new ColumnDefinition(colName, type, nullable, dim, isPk));
                }

                if (!Match(TokenType.Comma))
                    break;
            }

            Consume(TokenType.CloseParen);
            Optional(TokenType.Semicolon);

            if (string.IsNullOrEmpty(primaryKey) && columns.Count > 0)
            {
                primaryKey = columns[0].Name;
            }

            return new CreateTableStatement(tableName, columns, primaryKey);
        }
        else if (Match(TokenType.Index))
        {
            string indexName = Consume(TokenType.Identifier).Text;
            Consume(TokenType.On);
            string tableName = Consume(TokenType.Identifier).Text;

            var indexType = IndexType.BPlusTree;
            if (Match(TokenType.Using))
            {
                string usingStr = Consume(TokenType.Identifier).Text.ToUpperInvariant();
                indexType = usingStr switch
                {
                    "HNSW" => IndexType.HnswVector,
                    "ART" => IndexType.AdaptiveRadixTree,
                    "ROARING_BITMAP" or "BITMAP" => IndexType.RoaringBitmap,
                    "BTREE" => IndexType.BPlusTree,
                    _ => throw new XqlParseException($"Unknown index type '{usingStr}'.", Peek().Line, Peek().Column)
                };
            }

            Consume(TokenType.OpenParen);
            var colNames = new List<string>();
            while (true)
            {
                colNames.Add(Consume(TokenType.Identifier).Text);
                if (!Match(TokenType.Comma))
                    break;
            }
            Consume(TokenType.CloseParen);

            Dictionary<string, string>? props = null;
            if (Match(TokenType.With))
            {
                props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                Consume(TokenType.OpenParen);
                while (true)
                {
                    string propKey = Consume(TokenType.Identifier).Text;
                    Consume(TokenType.Equal);
                    var valToken = Advance();
                    props[propKey] = valToken.Text;

                    if (!Match(TokenType.Comma))
                        break;
                }
                Consume(TokenType.CloseParen);
            }

            Optional(TokenType.Semicolon);
            return new CreateIndexStatement(indexName, tableName, indexType, colNames, props);
        }

        throw new XqlParseException("Expected TABLE or INDEX after CREATE.", Peek().Line, Peek().Column);
    }

    private StatementNode ParseDrop()
    {
        Consume(TokenType.Drop);
        Consume(TokenType.Table);
        string table = Consume(TokenType.Identifier).Text;
        Optional(TokenType.Semicolon);
        return new DropTableStatement(table);
    }

    private StatementNode ParseBegin()
    {
        Consume(TokenType.Begin);
        Optional(TokenType.Transaction);
        Optional(TokenType.Semicolon);
        return new TransactionStatement(TransactionAction.Begin);
    }

    // Expression Parsing with Precedence Climbing

    public ExpressionNode ParseExpression() => ParseOr();

    private ExpressionNode ParseOr()
    {
        var left = ParseAnd();
        while (Match(TokenType.Or))
        {
            var right = ParseAnd();
            left = new BinaryExpression(left, BinaryOp.Or, right);
        }
        return left;
    }

    private ExpressionNode ParseAnd()
    {
        var left = ParseComparison();
        while (Match(TokenType.And))
        {
            var right = ParseComparison();
            left = new BinaryExpression(left, BinaryOp.And, right);
        }
        return left;
    }

    private ExpressionNode ParseComparison()
    {
        var left = ParseAdditive();

        while (true)
        {
            if (Match(TokenType.Equal)) left = new BinaryExpression(left, BinaryOp.Equal, ParseAdditive());
            else if (Match(TokenType.NotEqual)) left = new BinaryExpression(left, BinaryOp.NotEqual, ParseAdditive());
            else if (Match(TokenType.LessThan)) left = new BinaryExpression(left, BinaryOp.LessThan, ParseAdditive());
            else if (Match(TokenType.LessThanOrEqual)) left = new BinaryExpression(left, BinaryOp.LessThanOrEqual, ParseAdditive());
            else if (Match(TokenType.GreaterThan)) left = new BinaryExpression(left, BinaryOp.GreaterThan, ParseAdditive());
            else if (Match(TokenType.GreaterThanOrEqual)) left = new BinaryExpression(left, BinaryOp.GreaterThanOrEqual, ParseAdditive());
            else if (Match(TokenType.L2Distance)) left = new VectorDistanceExpression(left, VectorDistanceMetric.L2, ParseAdditive());
            else if (Match(TokenType.DotProduct)) left = new VectorDistanceExpression(left, VectorDistanceMetric.DotProduct, ParseAdditive());
            else if (Match(TokenType.CosineDistance)) left = new VectorDistanceExpression(left, VectorDistanceMetric.Cosine, ParseAdditive());
            else if (Match(TokenType.Is))
            {
                if (Match(TokenType.Not))
                {
                    Consume(TokenType.Null);
                    left = new UnaryExpression(UnaryOp.IsNotNull, left);
                }
                else
                {
                    Consume(TokenType.Null);
                    left = new UnaryExpression(UnaryOp.IsNull, left);
                }
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private ExpressionNode ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (true)
        {
            if (Match(TokenType.Plus)) left = new BinaryExpression(left, BinaryOp.Add, ParseMultiplicative());
            else if (Match(TokenType.Minus)) left = new BinaryExpression(left, BinaryOp.Subtract, ParseMultiplicative());
            else break;
        }
        return left;
    }

    private ExpressionNode ParseMultiplicative()
    {
        var left = ParseUnary();
        while (true)
        {
            if (Match(TokenType.Asterisk)) left = new BinaryExpression(left, BinaryOp.Multiply, ParseUnary());
            else if (Match(TokenType.Slash)) left = new BinaryExpression(left, BinaryOp.Divide, ParseUnary());
            else break;
        }
        return left;
    }

    private ExpressionNode ParseUnary()
    {
        if (Match(TokenType.Minus))
        {
            return new UnaryExpression(UnaryOp.Negate, ParseUnary());
        }
        if (Match(TokenType.Not))
        {
            return new UnaryExpression(UnaryOp.Not, ParseUnary());
        }
        return ParsePrimary();
    }

    private ExpressionNode ParsePrimary()
    {
        var tok = Peek();

        if (Match(TokenType.OpenParen))
        {
            var expr = ParseExpression();
            Consume(TokenType.CloseParen);
            return expr;
        }

        if (tok.Type == TokenType.IntegerLiteral)
        {
            Advance();
            return new LiteralExpression(SlateValue.FromInt64(tok.IntValue));
        }

        if (tok.Type == TokenType.FloatLiteral)
        {
            Advance();
            return new LiteralExpression(SlateValue.FromFloat64(tok.FloatValue));
        }

        if (tok.Type == TokenType.StringLiteral)
        {
            Advance();
            return new LiteralExpression(SlateValue.FromString(tok.Text));
        }

        if (tok.Type == TokenType.VectorLiteral)
        {
            Advance();
            return new LiteralExpression(SlateValue.FromVector(tok.VectorValue!));
        }

        if (Match(TokenType.True))
        {
            return new LiteralExpression(SlateValue.FromBoolean(true));
        }

        if (Match(TokenType.False))
        {
            return new LiteralExpression(SlateValue.FromBoolean(false));
        }

        if (Match(TokenType.Null))
        {
            return new LiteralExpression(SlateValue.Null);
        }

        if (tok.Type == TokenType.Identifier)
        {
            Advance();
            string name = tok.Text;

            // Check if function call
            if (Match(TokenType.OpenParen))
            {
                var args = new List<ExpressionNode>();
                if (Match(TokenType.Asterisk))
                {
                    args.Add(new ColumnReferenceExpression(null, "*"));
                }
                else if (Peek().Type != TokenType.CloseParen)
                {
                    while (true)
                    {
                        args.Add(ParseExpression());
                        if (!Match(TokenType.Comma))
                            break;
                    }
                }
                Consume(TokenType.CloseParen);
                return new FunctionCallExpression(name, args);
            }

            // Check if qualified column table.column
            if (Match(TokenType.Dot))
            {
                string col = Consume(TokenType.Identifier).Text;
                return new ColumnReferenceExpression(name, col);
            }

            return new ColumnReferenceExpression(null, name);
        }

        throw new XqlParseException($"Unexpected expression token '{tok.Text}'.", tok.Line, tok.Column);
    }

    private Token Peek(int offset = 0)
    {
        int target = _pos + offset;
        return target < _tokens.Count ? _tokens[target] : _tokens[^1];
    }

    private Token Advance()
    {
        var token = Peek();
        if (_pos < _tokens.Count) _pos++;
        return token;
    }

    private bool Match(TokenType type)
    {
        if (Peek().Type == type)
        {
            Advance();
            return true;
        }
        return false;
    }

    private bool Optional(TokenType type)
    {
        if (Peek().Type == type)
        {
            Advance();
            return true;
        }
        return false;
    }

    private Token Consume(TokenType type)
    {
        var token = Peek();
        if (token.Type != type)
        {
            throw new XqlParseException($"Expected token '{type}' but found '{token.Type}' ('{token.Text}').", token.Line, token.Column);
        }
        return Advance();
    }
}
