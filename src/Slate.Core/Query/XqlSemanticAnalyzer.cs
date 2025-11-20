using Slate.Core.Catalog;
using Slate.Core.Primitives;

namespace Slate.Core.Query;

public sealed class SemanticException : Exception
{
    public SemanticException(string message) : base(message) { }
}

public sealed class SemanticAnalysisResult
{
    public StatementNode Statement { get; }
    public Dictionary<ExpressionNode, DataType> InferredTypes { get; } = new();
    public Dictionary<ExpressionNode, int> VectorDimensions { get; } = new();

    public SemanticAnalysisResult(StatementNode statement)
    {
        Statement = statement;
    }
}

/// <summary>
/// Semantic analyzer and type checker for XQL statements against a live schema catalog.
/// Enforces type compatibility, resolves wildcard projections, and validates vector dimensions.
/// </summary>
public sealed class XqlSemanticAnalyzer
{
    private readonly CatalogManager _catalog;

    public XqlSemanticAnalyzer(CatalogManager catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public SemanticAnalysisResult Analyze(StatementNode stmt)
    {
        var result = new SemanticAnalysisResult(stmt);

        switch (stmt)
        {
            case SelectStatement select:
                AnalyzeSelect(select, result);
                break;
            case InsertStatement insert:
                AnalyzeInsert(insert, result);
                break;
            case UpdateStatement update:
                AnalyzeUpdate(update, result);
                break;
            case DeleteStatement delete:
                AnalyzeDelete(delete, result);
                break;
            case CreateTableStatement createTable:
                AnalyzeCreateTable(createTable);
                break;
            case CreateIndexStatement createIndex:
                AnalyzeCreateIndex(createIndex);
                break;
            case DropTableStatement dropTable:
                AnalyzeDropTable(dropTable);
                break;
            case TransactionStatement:
                break;
            case ExplainStatement explain:
                Analyze(explain.TargetStatement);
                break;
            default:
                throw new SemanticException($"Unsupported statement type '{stmt.GetType().Name}'.");
        }

        return result;
    }

    private void AnalyzeSelect(SelectStatement select, SemanticAnalysisResult result)
    {
        TableMetadata? fromMeta = null;
        if (!string.IsNullOrEmpty(select.FromTable))
        {
            fromMeta = _catalog.GetTable(select.FromTable)
                ?? throw new SemanticException($"Table '{select.FromTable}' does not exist in catalog.");
        }

        // Handle wildcard '*' expansion if present
        if (select.Projections.Count == 1 &&
            select.Projections[0].Expression is ColumnReferenceExpression colRef &&
            colRef.ColumnName == "*")
        {
            if (fromMeta == null)
                throw new SemanticException("Wildcard '*' projection requires a FROM table.");

            select.Projections.Clear();
            foreach (var col in fromMeta.Schema.Columns)
            {
                select.Projections.Add(new SelectItem(new ColumnReferenceExpression(fromMeta.Schema.TableName, col.Name), col.Name));
            }
        }

        // Validate all projections
        foreach (var proj in select.Projections)
        {
            InferAndValidateType(proj.Expression, fromMeta, result);
        }

        // Validate WHERE predicate
        if (select.Where != null)
        {
            var whereType = InferAndValidateType(select.Where, fromMeta, result);
            if (whereType != DataType.Boolean)
            {
                throw new SemanticException($"WHERE predicate must evaluate to Boolean, found '{whereType}'.");
            }
        }

        // Validate ORDER BY
        if (select.OrderBy != null)
        {
            foreach (var order in select.OrderBy)
            {
                InferAndValidateType(order.Expression, fromMeta, result);
            }
        }
    }

    private void AnalyzeInsert(InsertStatement insert, SemanticAnalysisResult result)
    {
        var meta = _catalog.GetTable(insert.TableName)
            ?? throw new SemanticException($"Table '{insert.TableName}' does not exist in catalog.");

        var targetColumns = insert.ColumnNames != null && insert.ColumnNames.Count > 0
            ? insert.ColumnNames.Select(name => meta.Schema.FindColumn(name)
                ?? throw new SemanticException($"Column '{name}' not found in table '{insert.TableName}'.")).ToList()
            : meta.Schema.Columns.ToList();

        foreach (var row in insert.Rows)
        {
            if (row.Count != targetColumns.Count)
            {
                throw new SemanticException($"INSERT row length ({row.Count}) does not match target column count ({targetColumns.Count}).");
            }

            for (int i = 0; i < row.Count; i++)
            {
                var exprType = InferAndValidateType(row[i], meta, result);
                var colType = targetColumns[i].Type;

                if (!AreTypesCompatible(colType, exprType))
                {
                    throw new SemanticException($"Type mismatch on column '{targetColumns[i].Name}': cannot assign '{exprType}' to '{colType}'.");
                }
            }
        }
    }

    private void AnalyzeUpdate(UpdateStatement update, SemanticAnalysisResult result)
    {
        var meta = _catalog.GetTable(update.TableName)
            ?? throw new SemanticException($"Table '{update.TableName}' does not exist in catalog.");

        foreach (var assign in update.Assignments)
        {
            var col = meta.Schema.FindColumn(assign.ColumnName)
                ?? throw new SemanticException($"Column '{assign.ColumnName}' not found in table '{update.TableName}'.");

            var exprType = InferAndValidateType(assign.Value, meta, result);
            if (!AreTypesCompatible(col.Type, exprType))
            {
                throw new SemanticException($"Cannot assign '{exprType}' to column '{assign.ColumnName}' of type '{col.Type}'.");
            }
        }

        if (update.Where != null)
        {
            var whereType = InferAndValidateType(update.Where, meta, result);
            if (whereType != DataType.Boolean)
            {
                throw new SemanticException($"UPDATE WHERE predicate must evaluate to Boolean, found '{whereType}'.");
            }
        }
    }

    private void AnalyzeDelete(DeleteStatement delete, SemanticAnalysisResult result)
    {
        var meta = _catalog.GetTable(delete.TableName)
            ?? throw new SemanticException($"Table '{delete.TableName}' does not exist in catalog.");

        if (delete.Where != null)
        {
            var whereType = InferAndValidateType(delete.Where, meta, result);
            if (whereType != DataType.Boolean)
            {
                throw new SemanticException($"DELETE WHERE predicate must evaluate to Boolean, found '{whereType}'.");
            }
        }
    }

    private void AnalyzeCreateTable(CreateTableStatement createTable)
    {
        if (_catalog.TableExists(createTable.TableName))
            throw new SemanticException($"Table '{createTable.TableName}' already exists.");

        if (string.IsNullOrEmpty(createTable.PrimaryKeyColumn))
            throw new SemanticException($"Table '{createTable.TableName}' must specify a PRIMARY KEY column.");

        if (!createTable.Columns.Any(c => c.Name.Equals(createTable.PrimaryKeyColumn, StringComparison.OrdinalIgnoreCase)))
            throw new SemanticException($"PRIMARY KEY column '{createTable.PrimaryKeyColumn}' is not defined in column list.");
    }

    private void AnalyzeCreateIndex(CreateIndexStatement createIndex)
    {
        var meta = _catalog.GetTable(createIndex.TableName)
            ?? throw new SemanticException($"Table '{createIndex.TableName}' does not exist in catalog.");

        foreach (var col in createIndex.ColumnNames)
        {
            if (meta.Schema.FindColumn(col) == null)
                throw new SemanticException($"Indexed column '{col}' does not exist in table '{createIndex.TableName}'.");
        }
    }

    private void AnalyzeDropTable(DropTableStatement dropTable)
    {
        if (!_catalog.TableExists(dropTable.TableName))
            throw new SemanticException($"Table '{dropTable.TableName}' does not exist.");
    }

    private DataType InferAndValidateType(ExpressionNode expr, TableMetadata? meta, SemanticAnalysisResult result)
    {
        switch (expr)
        {
            case LiteralExpression lit:
                result.InferredTypes[expr] = lit.Value.Type;
                if (lit.Value.Type == DataType.VectorFloat32)
                {
                    result.VectorDimensions[expr] = lit.Value.AsVector().Length;
                }
                return lit.Value.Type;

            case ColumnReferenceExpression colRef:
                if (meta == null)
                    throw new SemanticException($"Cannot resolve column '{colRef.ColumnName}' without a table context.");

                var col = meta.Schema.FindColumn(colRef.ColumnName)
                    ?? throw new SemanticException($"Column '{colRef.ColumnName}' does not exist in table '{meta.Schema.TableName}'.");

                result.InferredTypes[expr] = col.Type;
                if (col.Type == DataType.VectorFloat32)
                {
                    result.VectorDimensions[expr] = col.VectorDimension;
                }
                return col.Type;

            case BinaryExpression bin:
                var leftType = InferAndValidateType(bin.Left, meta, result);
                var rightType = InferAndValidateType(bin.Right, meta, result);

                switch (bin.Op)
                {
                    case BinaryOp.And:
                    case BinaryOp.Or:
                        if (leftType != DataType.Boolean || rightType != DataType.Boolean)
                            throw new SemanticException($"Logical operation '{bin.Op}' requires Boolean operands.");
                        result.InferredTypes[expr] = DataType.Boolean;
                        return DataType.Boolean;

                    case BinaryOp.Equal:
                    case BinaryOp.NotEqual:
                    case BinaryOp.LessThan:
                    case BinaryOp.LessThanOrEqual:
                    case BinaryOp.GreaterThan:
                    case BinaryOp.GreaterThanOrEqual:
                        if (!AreTypesComparable(leftType, rightType))
                            throw new SemanticException($"Cannot compare incompatible types '{leftType}' and '{rightType}'.");
                        result.InferredTypes[expr] = DataType.Boolean;
                        return DataType.Boolean;

                    case BinaryOp.Add:
                    case BinaryOp.Subtract:
                    case BinaryOp.Multiply:
                    case BinaryOp.Divide:
                        if (!IsNumeric(leftType) || !IsNumeric(rightType))
                            throw new SemanticException($"Arithmetic operation '{bin.Op}' requires numeric operands.");
                        var numType = (leftType == DataType.Float64 || rightType == DataType.Float64) ? DataType.Float64 : DataType.Int64;
                        result.InferredTypes[expr] = numType;
                        return numType;
                }
                break;

            case VectorDistanceExpression vecDist:
                var lType = InferAndValidateType(vecDist.Left, meta, result);
                var rType = InferAndValidateType(vecDist.Right, meta, result);

                if (lType != DataType.VectorFloat32 || rType != DataType.VectorFloat32)
                    throw new SemanticException($"Vector distance operation '{vecDist.Metric}' requires VectorFloat32 operands.");

                result.VectorDimensions.TryGetValue(vecDist.Left, out int lDim);
                result.VectorDimensions.TryGetValue(vecDist.Right, out int rDim);

                if (lDim > 0 && rDim > 0 && lDim != rDim)
                {
                    throw new SemanticException($"Vector dimension mismatch for '{vecDist.Metric}': left dimension is {lDim}, right dimension is {rDim}.");
                }

                result.InferredTypes[expr] = DataType.Float32;
                return DataType.Float32;

            case UnaryExpression un:
                var opType = InferAndValidateType(un.Operand, meta, result);
                if (un.Op == UnaryOp.Not)
                {
                    if (opType != DataType.Boolean)
                        throw new SemanticException("NOT operator requires Boolean operand.");
                    result.InferredTypes[expr] = DataType.Boolean;
                    return DataType.Boolean;
                }
                if (un.Op == UnaryOp.Negate)
                {
                    if (!IsNumeric(opType))
                        throw new SemanticException("Negation operator requires numeric operand.");
                    result.InferredTypes[expr] = opType;
                    return opType;
                }
                if (un.Op == UnaryOp.IsNull || un.Op == UnaryOp.IsNotNull)
                {
                    result.InferredTypes[expr] = DataType.Boolean;
                    return DataType.Boolean;
                }
                break;

            case FunctionCallExpression fn:
                // Count, Sum, Avg, Min, Max
                string upperFn = fn.FunctionName.ToUpperInvariant();
                if (upperFn == "COUNT")
                {
                    result.InferredTypes[expr] = DataType.Int64;
                    return DataType.Int64;
                }
                if (upperFn == "SUM" || upperFn == "AVG" || upperFn == "MIN" || upperFn == "MAX")
                {
                    if (fn.Arguments.Count != 1)
                        throw new SemanticException($"Aggregate '{upperFn}' takes exactly 1 argument.");
                    var argType = InferAndValidateType(fn.Arguments[0], meta, result);
                    if (!IsNumeric(argType))
                        throw new SemanticException($"Aggregate '{upperFn}' requires numeric argument.");
                    var resType = (upperFn == "AVG" || argType == DataType.Float64) ? DataType.Float64 : DataType.Int64;
                    result.InferredTypes[expr] = resType;
                    return resType;
                }
                break;
        }

        return DataType.String;
    }

    private static bool IsNumeric(DataType type) =>
        type == DataType.Int8 || type == DataType.Int16 || type == DataType.Int32 ||
        type == DataType.Int64 || type == DataType.Float32 || type == DataType.Float64;

    private static bool AreTypesCompatible(DataType target, DataType source)
    {
        if (target == source) return true;
        if (IsNumeric(target) && IsNumeric(source)) return true;
        if (target == DataType.String) return true;
        return false;
    }

    private static bool AreTypesComparable(DataType left, DataType right)
    {
        if (left == right) return true;
        if (IsNumeric(left) && IsNumeric(right)) return true;
        return false;
    }
}
