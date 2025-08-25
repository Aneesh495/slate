namespace Slate.Core.Primitives;

/// <summary>
/// A columnar vector batch representing a vertical slice of record columns.
/// Serves as the fundamental unit of data transfer through vectorized physical execution operators.
/// </summary>
public sealed class VectorBatch
{
    public TableSchema Schema { get; }
    public int RowCount { get; }
    public ColumnVector[] Columns { get; }

    /// <summary>
    /// Optional selection vector indexing rows that passed upstream filter evaluation.
    /// If null, all rows [0, RowCount) are active.
    /// </summary>
    public int[]? SelectionVector { get; set; }
    public int SelectedCount => SelectionVector?.Length ?? RowCount;

    public VectorBatch(TableSchema schema, ColumnVector[] columns, int rowCount, int[]? selectionVector = null)
    {
        if (columns.Length != schema.ColumnCount)
            throw new ArgumentException($"Column count mismatch. Expected {schema.ColumnCount}, got {columns.Length}.", nameof(columns));

        Schema = schema;
        Columns = columns;
        RowCount = rowCount;
        SelectionVector = selectionVector;
    }

    public static VectorBatch CreateEmpty(TableSchema schema, int capacity)
    {
        var cols = new ColumnVector[schema.ColumnCount];
        for (int i = 0; i < schema.ColumnCount; i++)
        {
            cols[i] = new ColumnVector(schema[i].Type, capacity);
        }
        return new VectorBatch(schema, cols, 0);
    }

    public ColumnVector this[int ordinal] => Columns[ordinal];
    public ColumnVector this[string columnName] => Columns[Schema[columnName].Ordinal];

    public SlateValue GetValue(int columnOrdinal, int rowIndex)
    {
        int actualRow = SelectionVector is not null ? SelectionVector[rowIndex] : rowIndex;
        return Columns[columnOrdinal].GetValue(actualRow);
    }
}
