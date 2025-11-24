using Slate.Core.Catalog;
using Slate.Core.Primitives;
using Slate.Core.Query;
using Xunit;

namespace Slate.Tests;

public sealed class LogicalPlannerTests
{
    private CatalogManager CreateCatalog()
    {
        var catalog = new CatalogManager();
        var schema = new TableSchema("documents", new[]
        {
            new ColumnDescriptor("id", DataType.Int64, isNullable: false, ordinal: 0),
            new ColumnDescriptor("title", DataType.String, isNullable: false, ordinal: 1),
            new ColumnDescriptor("embedding", DataType.VectorFloat32, isNullable: false, ordinal: 2, vectorDimension: 3)
        }, primaryKeyName: "id");

        catalog.CreateTable(schema, primaryKeyColumn: "id");
        return catalog;
    }

    [Fact]
    public void LogicalPlanner_PredicatePushdown_PushesFilterIntoScan()
    {
        var catalog = CreateCatalog();
        var planner = new LogicalPlanner(catalog);

        var stmt = XqlParser.Parse("SELECT id, title FROM documents WHERE id = 42;");
        var plan = planner.Plan(stmt);

        var project = Assert.IsType<LogicalProject>(plan);
        var scan = Assert.IsType<LogicalScan>(project.Input);

        Assert.Equal("documents", scan.TableName);
        Assert.NotNull(scan.PushedPredicate);
        var bin = Assert.IsType<BinaryExpression>(scan.PushedPredicate);
        Assert.Equal(BinaryOp.Equal, bin.Op);
    }

    [Fact]
    public void LogicalPlanner_VectorKnnPattern_CollapsesToLogicalVectorKnn()
    {
        var catalog = CreateCatalog();
        var planner = new LogicalPlanner(catalog);

        var stmt = XqlParser.Parse("SELECT id FROM documents ORDER BY embedding <-> [0.1, 0.2, 0.3] LIMIT 10;");
        var plan = planner.Plan(stmt);

        var project = Assert.IsType<LogicalProject>(plan);
        var knn = Assert.IsType<LogicalVectorKnn>(project.Input);

        Assert.Equal("embedding", knn.VectorColumn);
        Assert.Equal(VectorDistanceMetric.L2, knn.Metric);
        Assert.Equal(10, knn.K);
        Assert.Equal(3, knn.TargetVector.Length);

        var scan = Assert.IsType<LogicalScan>(knn.Input);
        Assert.Equal("documents", scan.TableName);
    }

    [Fact]
    public void LogicalPlanner_Explain_ProducesDescriptiveTree()
    {
        var catalog = CreateCatalog();
        var planner = new LogicalPlanner(catalog);

        var stmt = XqlParser.Parse("SELECT id FROM documents ORDER BY embedding <-> [0.1, 0.2, 0.3] LIMIT 5;");
        var plan = planner.Plan(stmt);

        string explain = plan.Explain();
        Assert.Contains("LogicalProject", explain);
        Assert.Contains("LogicalVectorKnn", explain);
        Assert.Contains("LogicalScan", explain);
    }
}
