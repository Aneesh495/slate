using Slate.Core.Catalog;
using Slate.Core.Primitives;
using Xunit;

namespace Slate.Tests;

public sealed class CatalogTests
{
    [Fact]
    public void CatalogManager_CreateTableAndEvolution_BumpsVersion()
    {
        var catalog = new CatalogManager();
        var schema = new TableSchema("products", new[]
        {
            new ColumnDescriptor("id", DataType.Int64, isNullable: false, ordinal: 0),
            new ColumnDescriptor("title", DataType.String, isNullable: false, ordinal: 1)
        });

        catalog.CreateTable(schema, primaryKeyColumn: "id");
        Assert.True(catalog.TableExists("products"));

        var meta = catalog.GetTable("products");
        Assert.NotNull(meta);
        Assert.Equal(1u, meta.SchemaVersion);
        Assert.Equal(2, meta.Schema.ColumnCount);

        // Add a new column 'embedding'
        var embeddingCol = new ColumnDescriptor("embedding", DataType.VectorFloat32, isNullable: true, vectorDimension: 128);
        catalog.AddColumn("products", embeddingCol);

        var updatedMeta = catalog.GetTable("products");
        Assert.NotNull(updatedMeta);
        Assert.Equal(2u, updatedMeta.SchemaVersion);
        Assert.Equal(3, updatedMeta.Schema.ColumnCount);
        Assert.Equal("embedding", updatedMeta.Schema.Columns[2].Name);
        Assert.Equal(128, updatedMeta.Schema.Columns[2].VectorDimension);
    }

    [Fact]
    public void CatalogManager_AddIndexes_TracksSecondaryIndexes()
    {
        var catalog = new CatalogManager();
        var schema = new TableSchema("documents", new[]
        {
            new ColumnDescriptor("doc_id", DataType.Int64, isNullable: false, ordinal: 0),
            new ColumnDescriptor("category", DataType.String, isNullable: false, ordinal: 1),
            new ColumnDescriptor("vec", DataType.VectorFloat32, isNullable: false, ordinal: 2, vectorDimension: 64)
        });

        catalog.CreateTable(schema, primaryKeyColumn: "doc_id");

        var btreeIdx = new IndexDescriptor("idx_docs_category", IndexType.BPlusTree, new[] { "category" });
        catalog.AddIndex("documents", btreeIdx);

        var hnswIdx = new IndexDescriptor("idx_docs_vec", IndexType.HnswVector, new[] { "vec" }, new Dictionary<string, string>
        {
            ["M"] = "16",
            ["efConstruction"] = "100"
        });
        catalog.AddIndex("documents", hnswIdx);

        var indexes = catalog.GetIndexes("documents");
        Assert.Equal(2, indexes.Count);
        Assert.Contains(indexes, i => i.Name == "idx_docs_category" && i.Type == IndexType.BPlusTree);
        Assert.Contains(indexes, i => i.Name == "idx_docs_vec" && i.Type == IndexType.HnswVector);
    }

    [Fact]
    public void CatalogManager_Serialization_RoundTripsAccurately()
    {
        var catalog = new CatalogManager();
        var schema = new TableSchema("vectors", new[]
        {
            new ColumnDescriptor("id", DataType.Int64, isNullable: false, ordinal: 0),
            new ColumnDescriptor("vec", DataType.VectorFloat32, isNullable: false, ordinal: 1, vectorDimension: 32)
        });

        catalog.CreateTable(schema, primaryKeyColumn: "id");
        catalog.AddIndex("vectors", new IndexDescriptor("vec_idx", IndexType.HnswVector, new[] { "vec" }));

        byte[] serialized = catalog.SerializeJson();
        var restored = CatalogManager.DeserializeJson(serialized);

        Assert.True(restored.TableExists("vectors"));
        var restoredMeta = restored.GetTable("vectors")!;
        Assert.Equal("id", restoredMeta.PrimaryKeyColumn);
        Assert.Equal(2, restoredMeta.Schema.ColumnCount);
        Assert.Single(restored.GetIndexes("vectors"));
    }
}
