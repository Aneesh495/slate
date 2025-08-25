using Slate.Core.Primitives;
using Xunit;

namespace Slate.Tests;

public sealed class PrimitivesTests
{
    [Fact]
    public void SlateValue_NumericEquality_WorksAcrossWideningTypes()
    {
        var v1 = SlateValue.FromInt32(42);
        var v2 = SlateValue.FromInt64(42L);
        var v3 = SlateValue.FromFloat64(42.0);

        Assert.Equal(v1, v2);
        Assert.Equal(v2, v3);
        Assert.Equal(0, v1.CompareTo(v3));
    }

    [Fact]
    public void SlateValue_StringAndNull_HandleSemanticsCorrectly()
    {
        var nullVal = SlateValue.Null;
        var strVal = SlateValue.FromString("apollo");

        Assert.True(nullVal.IsNull);
        Assert.False(strVal.IsNull);
        Assert.Equal("apollo", strVal.AsString());
        Assert.Equal("NULL", nullVal.AsString());
    }

    [Fact]
    public void ColumnVector_AppendAndRead_MaintainsIntegrity()
    {
        var vec = new ColumnVector(DataType.Int64, 10);
        vec.AppendInt64(100);
        vec.AppendNull();
        vec.AppendInt64(300);

        Assert.Equal(3, vec.Length);
        Assert.False(vec.IsNull(0));
        Assert.True(vec.IsNull(1));
        Assert.False(vec.IsNull(2));
        Assert.Equal(100L, vec.GetInt64(0));
        Assert.Equal(300L, vec.GetInt64(2));
    }

    [Fact]
    public void TableSchema_LookupAndValidation_Succeeds()
    {
        var schema = new TableSchema("trades", new[]
        {
            new ColumnDescriptor("id", DataType.Int64, isNullable: false),
            new ColumnDescriptor("symbol", DataType.String, isNullable: false),
            new ColumnDescriptor("price", DataType.Float64, isNullable: false),
            new ColumnDescriptor("qty", DataType.Int32, isNullable: false)
        }, primaryKeyName: "id");

        Assert.Equal(4, schema.ColumnCount);
        Assert.Equal(0, schema.PrimaryKeyOrdinal);
        Assert.Equal(DataType.Float64, schema["price"].Type);
        Assert.True(schema.ContainsColumn("symbol"));
        Assert.False(schema.ContainsColumn("nonexistent"));
    }
}
