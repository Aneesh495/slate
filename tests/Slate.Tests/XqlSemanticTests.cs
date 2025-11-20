using Slate.Core.Catalog;
using Slate.Core.Primitives;
using Slate.Core.Query;
using Xunit;

namespace Slate.Tests;

public sealed class XqlSemanticTests
{
    private CatalogManager CreateTestCatalog()
    {
        var catalog = new CatalogManager();
        var schema = new TableSchema("articles", new[]
        {
            new ColumnDescriptor("id", DataType.Int64, isNullable: false, ordinal: 0),
            new ColumnDescriptor("title", DataType.String, isNullable: false, ordinal: 1),
            new ColumnDescriptor("views", DataType.Int64, isNullable: false, ordinal: 2),
            new ColumnDescriptor("embedding", DataType.VectorFloat32, isNullable: false, ordinal: 3, vectorDimension: 4)
        }, primaryKeyName: "id");

        catalog.CreateTable(schema, primaryKeyColumn: "id");
        return catalog;
    }

    [Fact]
    public void XqlSemanticAnalyzer_ValidSelect_InfersTypesCorrectly()
    {
        var catalog = CreateTestCatalog();
        var analyzer = new XqlSemanticAnalyzer(catalog);

        var stmt = XqlParser.Parse("SELECT id, title FROM articles WHERE views > 100 ORDER BY views DESC;");
        var result = analyzer.Analyze(stmt);

        Assert.NotNull(result);
        var select = Assert.IsType<SelectStatement>(result.Statement);
        Assert.Equal(2, select.Projections.Count);
    }

    [Fact]
    public void XqlSemanticAnalyzer_WildcardExpansion_ExpandsAllColumns()
    {
        var catalog = CreateTestCatalog();
        var analyzer = new XqlSemanticAnalyzer(catalog);

        var stmt = XqlParser.Parse("SELECT * FROM articles;");
        var result = analyzer.Analyze(stmt);

        var select = Assert.IsType<SelectStatement>(result.Statement);
        Assert.Equal(4, select.Projections.Count);
        Assert.Equal("id", select.Projections[0].Alias);
        Assert.Equal("title", select.Projections[1].Alias);
        Assert.Equal("views", select.Projections[2].Alias);
        Assert.Equal("embedding", select.Projections[3].Alias);
    }

    [Fact]
    public void XqlSemanticAnalyzer_VectorDistance_ValidatesDimensions()
    {
        var catalog = CreateTestCatalog();
        var analyzer = new XqlSemanticAnalyzer(catalog);

        // Valid dimension: 4 == 4
        var validStmt = XqlParser.Parse("SELECT id FROM articles ORDER BY embedding <-> [0.1, 0.2, 0.3, 0.4] LIMIT 5;");
        var validResult = analyzer.Analyze(validStmt);
        Assert.NotNull(validResult);

        // Mismatched dimension: 4 != 2
        var invalidStmt = XqlParser.Parse("SELECT id FROM articles ORDER BY embedding <-> [0.1, 0.2] LIMIT 5;");
        var ex = Assert.Throws<SemanticException>(() => analyzer.Analyze(invalidStmt));
        Assert.Contains("dimension mismatch", ex.Message);
    }

    [Fact]
    public void XqlSemanticAnalyzer_NonExistentTableOrColumn_ThrowsSemanticException()
    {
        var catalog = CreateTestCatalog();
        var analyzer = new XqlSemanticAnalyzer(catalog);

        var badTableStmt = XqlParser.Parse("SELECT id FROM non_existent_table;");
        Assert.Throws<SemanticException>(() => analyzer.Analyze(badTableStmt));

        var badColStmt = XqlParser.Parse("SELECT nonexistent_column FROM articles;");
        Assert.Throws<SemanticException>(() => analyzer.Analyze(badColStmt));
    }
}
