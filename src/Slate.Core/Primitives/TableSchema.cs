using System.Collections.Immutable;

namespace Slate.Core.Primitives;

/// <summary>
/// An immutable, validated schema for a Slate relational table or materialized view.
/// Provides O(1) column resolution by name or ordinal.
/// </summary>
public sealed class TableSchema
{
    private readonly ImmutableArray<ColumnDescriptor> _columns;
    private readonly Dictionary<string, int> _nameToOrdinal;
    private readonly int _primaryKeyOrdinal;

    public string TableName { get; }
    public ImmutableArray<ColumnDescriptor> Columns => _columns;
    public int ColumnCount => _columns.Length;
    public int PrimaryKeyOrdinal => _primaryKeyOrdinal;
    public ColumnDescriptor? PrimaryKey => _primaryKeyOrdinal >= 0 ? _columns[_primaryKeyOrdinal] : null;

    public TableSchema(string tableName, IEnumerable<ColumnDescriptor> columns, string? primaryKeyName = null)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));

        TableName = tableName.Trim();
        var cols = columns.ToList();
        if (cols.Count == 0)
            throw new ArgumentException("Table schema must contain at least one column.", nameof(columns));

        var builder = ImmutableArray.CreateBuilder<ColumnDescriptor>(cols.Count);
        _nameToOrdinal = new Dictionary<string, int>(cols.Count, StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < cols.Count; i++)
        {
            var col = cols[i];
            var normalized = new ColumnDescriptor(col.Name, col.Type, col.IsNullable, i, col.VectorDimension);
            if (_nameToOrdinal.ContainsKey(normalized.Name))
                throw new ArgumentException($"Duplicate column name '{normalized.Name}' in schema.", nameof(columns));

            _nameToOrdinal[normalized.Name] = i;
            builder.Add(normalized);
        }

        _columns = builder.ToImmutable();

        if (primaryKeyName is not null)
        {
            if (!_nameToOrdinal.TryGetValue(primaryKeyName, out _primaryKeyOrdinal))
                throw new ArgumentException($"Primary key column '{primaryKeyName}' was not found in table schema.", nameof(primaryKeyName));
        }
        else
        {
            _primaryKeyOrdinal = -1;
        }
    }

    public ColumnDescriptor this[int ordinal] => _columns[ordinal];
    public ColumnDescriptor this[string name] => _columns[_nameToOrdinal[name]];

    public bool TryGetOrdinal(string name, out int ordinal) =>
        _nameToOrdinal.TryGetValue(name, out ordinal);

    public bool ContainsColumn(string name) =>
        _nameToOrdinal.ContainsKey(name);
}
