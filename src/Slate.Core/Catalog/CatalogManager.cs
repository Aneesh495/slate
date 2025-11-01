using System.Collections.Concurrent;
using System.Text.Json;
using Slate.Core.Primitives;

namespace Slate.Core.Catalog;

public enum IndexType : byte
{
    BPlusTree = 1,
    AdaptiveRadixTree = 2,
    RoaringBitmap = 3,
    HnswVector = 4
}

public sealed record IndexDescriptor(
    string Name,
    IndexType Type,
    string[] ColumnNames,
    Dictionary<string, string>? Properties = null);

public sealed class TableMetadata
{
    public TableSchema Schema { get; internal set; }
    public string PrimaryKeyColumn { get; }
    public uint SchemaVersion { get; internal set; }
    public ConcurrentDictionary<string, IndexDescriptor> Indexes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public TableMetadata(TableSchema schema, string primaryKeyColumn, uint schemaVersion = 1)
    {
        Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        PrimaryKeyColumn = primaryKeyColumn ?? throw new ArgumentNullException(nameof(primaryKeyColumn));
        SchemaVersion = schemaVersion;
    }
}

/// <summary>
/// Thread-safe catalog manager tracking table schemas, versions, and secondary indexes.
/// </summary>
public sealed class CatalogManager
{
    private readonly ConcurrentDictionary<string, TableMetadata> _tables = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public void CreateTable(TableSchema schema, string primaryKeyColumn)
    {
        if (schema == null)
            throw new ArgumentNullException(nameof(schema));

        var col = schema.FindColumn(primaryKeyColumn);
        if (col == null)
            throw new ArgumentException($"Primary key column '{primaryKeyColumn}' does not exist in schema.", nameof(primaryKeyColumn));

        lock (_lock)
        {
            if (_tables.ContainsKey(schema.TableName))
                throw new InvalidOperationException($"Table '{schema.TableName}' already exists in catalog.");

            var metadata = new TableMetadata(schema, primaryKeyColumn, schemaVersion: 1);
            _tables[schema.TableName] = metadata;
        }
    }

    public bool DropTable(string tableName)
    {
        lock (_lock)
        {
            return _tables.TryRemove(tableName, out _);
        }
    }

    public bool TableExists(string tableName) => _tables.ContainsKey(tableName);

    public TableMetadata? GetTable(string tableName)
    {
        _tables.TryGetValue(tableName, out var meta);
        return meta;
    }

    public IReadOnlyCollection<string> ListTables() => _tables.Keys.OrderBy(k => k).ToList();

    public void AddIndex(string tableName, IndexDescriptor index)
    {
        if (index == null)
            throw new ArgumentNullException(nameof(index));

        var meta = GetTable(tableName) ?? throw new KeyNotFoundException($"Table '{tableName}' does not exist.");

        foreach (var colName in index.ColumnNames)
        {
            if (meta.Schema.FindColumn(colName) == null)
                throw new ArgumentException($"Indexed column '{colName}' not found in table '{tableName}'.");
        }

        if (!meta.Indexes.TryAdd(index.Name, index))
            throw new InvalidOperationException($"Index '{index.Name}' already exists on table '{tableName}'.");
    }

    public bool DropIndex(string tableName, string indexName)
    {
        var meta = GetTable(tableName) ?? throw new KeyNotFoundException($"Table '{tableName}' does not exist.");
        return meta.Indexes.TryRemove(indexName, out _);
    }

    public IReadOnlyCollection<IndexDescriptor> GetIndexes(string tableName)
    {
        var meta = GetTable(tableName) ?? throw new KeyNotFoundException($"Table '{tableName}' does not exist.");
        return meta.Indexes.Values.ToList();
    }

    public void AddColumn(string tableName, ColumnDescriptor newColumn)
    {
        lock (_lock)
        {
            var meta = GetTable(tableName) ?? throw new KeyNotFoundException($"Table '{tableName}' does not exist.");

            if (meta.Schema.FindColumn(newColumn.Name) != null)
                throw new InvalidOperationException($"Column '{newColumn.Name}' already exists in table '{tableName}'.");

            var currentColumns = meta.Schema.Columns.ToList();
            var positionedColumn = new ColumnDescriptor(
                newColumn.Name,
                newColumn.Type,
                newColumn.IsNullable,
                ordinal: currentColumns.Count,
                vectorDimension: newColumn.VectorDimension);

            currentColumns.Add(positionedColumn);
            meta.Schema = new TableSchema(meta.Schema.TableName, currentColumns);
            meta.SchemaVersion++;
        }
    }

    public byte[] SerializeJson()
    {
        lock (_lock)
        {
            var export = new List<SerializableTableRecord>();
            foreach (var kvp in _tables)
            {
                var meta = kvp.Value;
                var cols = meta.Schema.Columns.Select(c => new SerializableColumn(
                    c.Name,
                    (int)c.Type,
                    c.IsNullable,
                    c.Ordinal,
                    c.VectorDimension)).ToList();

                var idxs = meta.Indexes.Values.Select(i => new SerializableIndex(
                    i.Name,
                    (int)i.Type,
                    i.ColumnNames,
                    i.Properties ?? new Dictionary<string, string>())).ToList();

                export.Add(new SerializableTableRecord(
                    meta.Schema.TableName,
                    meta.PrimaryKeyColumn,
                    meta.SchemaVersion,
                    cols,
                    idxs));
            }

            return JsonSerializer.SerializeToUtf8Bytes(export, new JsonSerializerOptions { WriteIndented = true });
        }
    }

    public static CatalogManager DeserializeJson(byte[] bytes)
    {
        var list = JsonSerializer.Deserialize<List<SerializableTableRecord>>(bytes)
                   ?? throw new InvalidOperationException("Failed to deserialize catalog records.");

        var catalog = new CatalogManager();
        foreach (var tbl in list)
        {
            var cols = tbl.Columns.Select(c => new ColumnDescriptor(
                c.Name,
                (DataType)c.Type,
                c.IsNullable,
                c.Ordinal,
                c.VectorDimension)).ToList();

            var schema = new TableSchema(tbl.TableName, cols);
            catalog.CreateTable(schema, tbl.PrimaryKeyColumn);
            var meta = catalog.GetTable(tbl.TableName)!;
            meta.SchemaVersion = tbl.SchemaVersion;

            foreach (var idx in tbl.Indexes)
            {
                catalog.AddIndex(tbl.TableName, new IndexDescriptor(
                    idx.Name,
                    (IndexType)idx.Type,
                    idx.ColumnNames,
                    idx.Properties));
            }
        }

        return catalog;
    }

    private sealed record SerializableColumn(string Name, int Type, bool IsNullable, int Ordinal, int VectorDimension);
    private sealed record SerializableIndex(string Name, int Type, string[] ColumnNames, Dictionary<string, string> Properties);
    private sealed record SerializableTableRecord(
        string TableName,
        string PrimaryKeyColumn,
        uint SchemaVersion,
        List<SerializableColumn> Columns,
        List<SerializableIndex> Indexes);
}
