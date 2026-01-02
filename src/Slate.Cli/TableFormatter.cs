using System.Text;
using Slate.Core.Primitives;
using Slate.Engine.Kernel;

namespace Slate.Cli;

/// <summary>
/// Formats relational and vector query results into structured ASCII/Unicode tables.
/// </summary>
public static class TableFormatter
{
    public static string FormatResult(QueryResult result, bool showTiming = true)
    {
        var sb = new StringBuilder();
        var schema = result.Schema;
        int colCount = schema.ColumnCount;

        if (colCount == 0)
        {
            sb.AppendLine("(Empty schema)");
            if (showTiming)
                sb.AppendLine($"Execution time: {result.ExecutionTimeMs:F2} ms");
            return sb.ToString();
        }

        // Calculate maximum width per column
        var colWidths = new int[colCount];
        for (int c = 0; c < colCount; c++)
        {
            colWidths[c] = Math.Max(schema.Columns[c].Name.Length, 6);
        }

        var formattedCells = new List<string[]>(result.Rows.Count);
        foreach (var row in result.Rows)
        {
            var cellRow = new string[colCount];
            for (int c = 0; c < colCount; c++)
            {
                var val = c < row.Length ? row[c] : SlateValue.Null;
                string text = FormatValue(val);
                cellRow[c] = text;
                colWidths[c] = Math.Max(colWidths[c], Math.Min(text.Length, 40));
            }
            formattedCells.Add(cellRow);
        }

        // Top border: +--------+--------+
        sb.AppendLine(BuildSeparator(colWidths, '+', '-'));

        // Header row
        sb.Append('|');
        for (int c = 0; c < colCount; c++)
        {
            sb.Append(' ');
            sb.Append(schema.Columns[c].Name.PadRight(colWidths[c]));
            sb.Append(" |");
        }
        sb.AppendLine();

        // Header separator
        sb.AppendLine(BuildSeparator(colWidths, '+', '='));

        // Data rows
        foreach (var row in formattedCells)
        {
            sb.Append('|');
            for (int c = 0; c < colCount; c++)
            {
                sb.Append(' ');
                string text = row[c].Length > 40 ? row[c].Substring(0, 37) + "..." : row[c];
                sb.Append(text.PadRight(colWidths[c]));
                sb.Append(" |");
            }
            sb.AppendLine();
        }

        // Bottom border
        sb.AppendLine(BuildSeparator(colWidths, '+', '-'));

        // Summary
        string rowStr = result.Rows.Count == 1 ? "1 row" : $"{result.Rows.Count} rows";
        if (showTiming)
        {
            sb.AppendLine($"({rowStr} in {result.ExecutionTimeMs:F2} ms)");
        }
        else
        {
            sb.AppendLine($"({rowStr})");
        }

        return sb.ToString();
    }

    private static string FormatValue(SlateValue val)
    {
        if (val.IsNull)
            return "NULL";

        if (val.Type == DataType.VectorFloat32)
        {
            var vec = val.AsVector();
            if (vec.Length <= 4)
            {
                return "[" + string.Join(", ", vec.Select(v => v.ToString("F3"))) + "]";
            }
            return $"[{vec[0]:F2}, {vec[1]:F2}, ... ({vec.Length} dims)]";
        }

        return val.AsString();
    }

    private static string BuildSeparator(int[] colWidths, char corner, char line)
    {
        var sb = new StringBuilder();
        sb.Append(corner);
        foreach (int w in colWidths)
        {
            sb.Append(new string(line, w + 2));
            sb.Append(corner);
        }
        return sb.ToString();
    }
}
