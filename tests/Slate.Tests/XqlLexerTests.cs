using Slate.Core.Query;
using Xunit;

namespace Slate.Tests;

public sealed class XqlLexerTests
{
    [Fact]
    public void XqlLexer_SelectQuery_ScansCorrectTokenStream()
    {
        string query = "SELECT id, name FROM users WHERE age >= 21 AND active = TRUE ORDER BY id ASC LIMIT 50;";
        var lexer = new XqlLexer(query);
        var tokens = lexer.Tokenize();

        Assert.Equal(TokenType.Select, tokens[0].Type);
        Assert.Equal(TokenType.Identifier, tokens[1].Type);
        Assert.Equal("id", tokens[1].Text);
        Assert.Equal(TokenType.Comma, tokens[2].Type);
        Assert.Equal(TokenType.Identifier, tokens[3].Type);
        Assert.Equal("name", tokens[3].Text);
        Assert.Equal(TokenType.From, tokens[4].Type);
        Assert.Equal(TokenType.Identifier, tokens[5].Type);
        Assert.Equal("users", tokens[5].Text);
        Assert.Equal(TokenType.Where, tokens[6].Type);

        Assert.Contains(tokens, t => t.Type == TokenType.GreaterThanOrEqual);
        Assert.Contains(tokens, t => t.Type == TokenType.IntegerLiteral && t.IntValue == 21);
        Assert.Contains(tokens, t => t.Type == TokenType.True);
        Assert.Contains(tokens, t => t.Type == TokenType.Limit);
        Assert.Contains(tokens, t => t.Type == TokenType.IntegerLiteral && t.IntValue == 50);
        Assert.Equal(TokenType.Eof, tokens.Last().Type);
    }

    [Fact]
    public void XqlLexer_VectorOperatorsAndLiterals_ScansVectorTokens()
    {
        string query = "SELECT doc_id FROM corpus ORDER BY embedding <-> [0.15, -0.42, 0.99] LIMIT 10;";
        var lexer = new XqlLexer(query);
        var tokens = lexer.Tokenize();

        var l2Token = tokens.Find(t => t.Type == TokenType.L2Distance);
        Assert.NotNull(l2Token);
        Assert.Equal("<->", l2Token.Text);

        var vecToken = tokens.Find(t => t.Type == TokenType.VectorLiteral);
        Assert.NotNull(vecToken);
        Assert.NotNull(vecToken.VectorValue);
        Assert.Equal(3, vecToken.VectorValue.Length);
        Assert.Equal(0.15f, vecToken.VectorValue[0], precision: 4);
        Assert.Equal(-0.42f, vecToken.VectorValue[1], precision: 4);
        Assert.Equal(0.99f, vecToken.VectorValue[2], precision: 4);
    }

    [Fact]
    public void XqlLexer_CommentsAndWhitespace_AreProperlyIgnored()
    {
        string query = @"
            -- Single line comment
            SELECT * /* inline comment */
            FROM items;
        ";
        var lexer = new XqlLexer(query);
        var tokens = lexer.Tokenize();

        Assert.Equal(TokenType.Select, tokens[0].Type);
        Assert.Equal(TokenType.Asterisk, tokens[1].Type);
        Assert.Equal(TokenType.From, tokens[2].Type);
        Assert.Equal("items", tokens[3].Text);
        Assert.Equal(TokenType.Semicolon, tokens[4].Type);
    }

    [Fact]
    public void XqlLexer_TransactionKeywords_ScansCorrectly()
    {
        string script = "BEGIN TRANSACTION; INSERT INTO t VALUES (1); COMMIT;";
        var lexer = new XqlLexer(script);
        var tokens = lexer.Tokenize();

        Assert.Equal(TokenType.Begin, tokens[0].Type);
        Assert.Equal(TokenType.Transaction, tokens[1].Type);
        Assert.Contains(tokens, t => t.Type == TokenType.Insert);
        Assert.Contains(tokens, t => t.Type == TokenType.Commit);
    }
}
