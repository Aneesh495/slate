namespace Slate.Core.Primitives;

/// <summary>
/// Immutable descriptor defining the physical and logical attributes of a table column.
/// </summary>
public sealed record ColumnDescriptor
{
    public string Name { get; }
    public DataType Type { get; }
    public bool IsNullable { get; }
    public int Ordinal { get; }
    public int VectorDimension { get; }

    public ColumnDescriptor(string name, DataType type, bool isNullable = true, int ordinal = 0, int vectorDimension = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Column name cannot be null or whitespace.", nameof(name));
        if (type == DataType.VectorFloat32 && vectorDimension <= 0)
            throw new ArgumentException("VectorFloat32 columns must specify a positive dimension.", nameof(vectorDimension));

        Name = name.Trim();
        Type = type;
        IsNullable = isNullable;
        Ordinal = ordinal;
        VectorDimension = vectorDimension;
    }

    public override string ToString() =>
        Type == DataType.VectorFloat32
            ? $"{Name}: VectorFloat32({VectorDimension}){(IsNullable ? " NULL" : " NOT NULL")}"
            : $"{Name}: {Type}{(IsNullable ? " NULL" : " NOT NULL")}";
}
