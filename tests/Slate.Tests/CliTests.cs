using Slate.Cli;
using Slate.Core.Primitives;
using Slate.Engine.Kernel;
using Xunit;

namespace Slate.Tests;

public sealed class CliTests
{
    [Fact]
    public void TableFormatter_FormatsRelationalTable_ContainsHeadersAndSeparators()
    {
        var schema = new TableSchema("users", new[]
        {
            new ColumnDescriptor("id", DataType.Int64, isNullable: false, ordinal: 0),
            new ColumnDescriptor("name", DataType.String, isNullable: false, ordinal: 1)
        });

        var rows = new List<SlateValue[]>
        {
            new[] { SlateValue.FromInt64(1), SlateValue.FromString("Alice") },
            new[] { SlateValue.FromInt64(2), SlateValue.FromString("Bob") }
        };

        var queryResult = new QueryResult(schema, rows, 2, 1.25);
        string formatted = TableFormatter.FormatResult(queryResult, showTiming: true);

        Assert.Contains("id", formatted);
        Assert.Contains("name", formatted);
        Assert.Contains("Alice", formatted);
        Assert.Contains("Bob", formatted);
        Assert.Contains("2 rows in 1.25 ms", formatted);
        Assert.Contains("+", formatted);
        Assert.Contains("|", formatted);
    }

    [Fact]
    public void TableFormatter_VectorColumn_TruncatesLargeDimensions()
    {
        var schema = new TableSchema("vecs", new[]
        {
            new ColumnDescriptor("v", DataType.VectorFloat32, isNullable: false, ordinal: 0, vectorDimension: 8)
        });

        float[] arr = { 0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f };
        var rows = new List<SlateValue[]> { new[] { SlateValue.FromVector(arr) } };

        var queryResult = new QueryResult(schema, rows, 1, 0.5);
        string formatted = TableFormatter.FormatResult(queryResult);

        Assert.Contains("8 dims", formatted);
    }
}
