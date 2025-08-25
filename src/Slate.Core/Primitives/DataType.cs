namespace Slate.Core.Primitives;

/// <summary>
/// Logical data types supported by the Slate columnar storage and query execution engine.
/// Fixed-width types are aligned to native CPU machine words where applicable.
/// </summary>
public enum DataType : byte
{
    Null = 0,
    Boolean = 1,
    Int8 = 2,
    Int16 = 3,
    Int32 = 4,
    Int64 = 5,
    Float32 = 6,
    Float64 = 7,
    String = 8,
    TimestampUtc = 9,
    Decimal128 = 10,
    Binary = 11,
    VectorFloat32 = 12
}

/// <summary>
/// Extension methods and size calculators for DataType.
/// </summary>
public static class DataTypeExtensions
{
    /// <summary>
    /// Returns the fixed width in bytes for fixed-length data types, or -1 for variable-length types.
    /// </summary>
    public static int GetFixedSize(this DataType type) => type switch
    {
        DataType.Boolean => 1,
        DataType.Int8 => 1,
        DataType.Int16 => 2,
        DataType.Int32 => 4,
        DataType.Int64 => 8,
        DataType.Float32 => 4,
        DataType.Float64 => 8,
        DataType.TimestampUtc => 8,
        DataType.Decimal128 => 16,
        _ => -1
    };

    /// <summary>
    /// Indicates whether the type has a constant byte length per value.
    /// </summary>
    public static bool IsFixedLength(this DataType type) => type.GetFixedSize() > 0;

    /// <summary>
    /// Indicates whether the type represents a continuous numeric quantity.
    /// </summary>
    public static bool IsNumeric(this DataType type) => type switch
    {
        DataType.Int8 or DataType.Int16 or DataType.Int32 or DataType.Int64 or
        DataType.Float32 or DataType.Float64 or DataType.Decimal128 => true,
        _ => false
    };
}
