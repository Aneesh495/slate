using Slate.Core.Catalog;
using Slate.Core.Primitives;
using Slate.Core.Query;
using Xunit;

namespace Slate.Tests;

public sealed class XqlParserTests
{
    [Fact]
    public void XqlParser_SelectStatement_ParsesAllClauses()
    {
        string sql = "SELECT id, name AS user_name FROM users WHERE age >= 18 AND status = 'active' ORDER BY id DESC LIMIT 25;";
        var stmt = Assert.IsType<SelectStatement>(XqlParser.Parse(sql));

        Assert.Equal("users", stmt.FromTable);
        Assert.Equal(2, stmt.Projections.Count);
        Assert.Equal("id", ((ColumnReferenceExpression)stmt.Projections[0].Expression).ColumnName);
        Assert.Equal("user_name", stmt.Projections[1].Alias);

        var where = Assert.IsType<BinaryExpression>(stmt.Where);
        Assert.Equal(BinaryOp.And, where.Op);

        Assert.NotNull(stmt.OrderBy);
        Assert.Single(stmt.OrderBy);
        Assert.False(stmt.OrderBy[0].Ascending); // DESC
        Assert.Equal(25, stmt.Limit);
    }

    [Fact]
    public void XqlParser_VectorKnnSearch_ParsesDistanceAndVectorLiteral()
    {
        string sql = "SELECT id FROM docs ORDER BY vec <-> [0.1, 0.2, 0.3] LIMIT 10;";
        var stmt = Assert.IsType<SelectStatement>(XqlParser.Parse(sql));

        Assert.Equal("docs", stmt.FromTable);
        Assert.NotNull(stmt.OrderBy);
        Assert.Single(stmt.OrderBy);

        var distExpr = Assert.IsType<VectorDistanceExpression>(stmt.OrderBy[0].Expression);
        Assert.Equal(VectorDistanceMetric.L2, distExpr.Metric);
        var leftCol = Assert.IsType<ColumnReferenceExpression>(distExpr.Left);
        Assert.Equal("vec", leftCol.ColumnName);

        var rightLit = Assert.IsType<LiteralExpression>(distExpr.Right);
        var vec = rightLit.Value.AsVector();
        Assert.Equal(3, vec.Length);
        Assert.Equal(0.1f, vec[0], precision: 4);
        Assert.Equal(10, stmt.Limit);
    }

    [Fact]
    public void XqlParser_CreateTable_ParsesTypesAndVectorDimensions()
    {
        string sql = "CREATE TABLE items (id INT PRIMARY KEY, title STRING NOT NULL, emb VECTOR(128));";
        var stmt = Assert.IsType<CreateTableStatement>(XqlParser.Parse(sql));

        Assert.Equal("items", stmt.TableName);
        Assert.Equal("id", stmt.PrimaryKeyColumn);
        Assert.Equal(3, stmt.Columns.Count);

        Assert.Equal("id", stmt.Columns[0].Name);
        Assert.Equal(DataType.Int64, stmt.Columns[0].Type);

        Assert.Equal("title", stmt.Columns[1].Name);
        Assert.Equal(DataType.String, stmt.Columns[1].Type);
        Assert.False(stmt.Columns[1].IsNullable);

        Assert.Equal("emb", stmt.Columns[2].Name);
        Assert.Equal(DataType.VectorFloat32, stmt.Columns[2].Type);
        Assert.Equal(128, stmt.Columns[2].VectorDimension);
    }

    [Fact]
    public void XqlParser_CreateIndex_ParsesHnswAndProperties()
    {
        string sql = "CREATE INDEX idx_emb ON items USING HNSW (emb) WITH (M = 16, efConstruction = 100);";
        var stmt = Assert.IsType<CreateIndexStatement>(XqlParser.Parse(sql));

        Assert.Equal("idx_emb", stmt.IndexName);
        Assert.Equal("items", stmt.TableName);
        Assert.Equal(IndexType.HnswVector, stmt.IndexType);
        Assert.Single(stmt.ColumnNames);
        Assert.Equal("emb", stmt.ColumnNames[0]);

        Assert.NotNull(stmt.Properties);
        Assert.Equal("16", stmt.Properties["M"]);
        Assert.Equal("100", stmt.Properties["efConstruction"]);
    }

    [Fact]
    public void XqlParser_InsertAndUpdateAndDelete_ParsesCorrectly()
    {
        var insertStmt = Assert.IsType<InsertStatement>(XqlParser.Parse("INSERT INTO users (id, name) VALUES (1, 'Alice'), (2, 'Bob');"));
        Assert.Equal("users", insertStmt.TableName);
        Assert.Equal(2, insertStmt.Rows.Count);

        var updateStmt = Assert.IsType<UpdateStatement>(XqlParser.Parse("UPDATE users SET name = 'Bob', age = 30 WHERE id = 2;"));
        Assert.Equal("users", updateStmt.TableName);
        Assert.Equal(2, updateStmt.Assignments.Count);
        Assert.NotNull(updateStmt.Where);

        var deleteStmt = Assert.IsType<DeleteStatement>(XqlParser.Parse("DELETE FROM users WHERE id = 1;"));
        Assert.Equal("users", deleteStmt.TableName);
        Assert.NotNull(deleteStmt.Where);
    }
}
