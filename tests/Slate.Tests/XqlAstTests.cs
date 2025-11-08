using Slate.Core.Catalog;
using Slate.Core.Primitives;
using Slate.Core.Query;
using Xunit;

namespace Slate.Tests;

public sealed class XqlAstTests
{
    [Fact]
    public void XqlAst_SelectWithVectorKnn_ConstructsValidAst()
    {
        var targetVec = new float[] { 0.1f, 0.2f, 0.3f };
        var stmt = new SelectStatement(
            Projections: new List<SelectItem>
            {
                new SelectItem(new ColumnReferenceExpression(null, "doc_id")),
                new SelectItem(new VectorDistanceExpression(
                    new ColumnReferenceExpression(null, "embedding"),
                    VectorDistanceMetric.L2,
                    new LiteralExpression(SlateValue.FromVector(targetVec))), "distance")
            },
            FromTable: "documents",
            OrderBy: new List<OrderByClause>
            {
                new OrderByClause(new ColumnReferenceExpression(null, "distance"), Ascending: true)
            },
            Limit: 10
        );

        Assert.Equal("documents", stmt.FromTable);
        Assert.Equal(2, stmt.Projections.Count);
        Assert.Equal("distance", stmt.Projections[1].Alias);
        Assert.Equal(10, stmt.Limit);
    }

    [Fact]
    public void XqlAst_CreateTableStatement_ContainsColumnDefinitions()
    {
        var stmt = new CreateTableStatement(
            TableName: "items",
            Columns: new List<ColumnDefinition>
            {
                new ColumnDefinition("id", DataType.Int64, IsNullable: false, IsPrimaryKey: true),
                new ColumnDefinition("feature", DataType.VectorFloat32, IsNullable: false, VectorDimension: 256)
            },
            PrimaryKeyColumn: "id"
        );

        Assert.Equal("items", stmt.TableName);
        Assert.Equal("id", stmt.PrimaryKeyColumn);
        Assert.Equal(2, stmt.Columns.Count);
        Assert.Equal(256, stmt.Columns[1].VectorDimension);
    }
}
