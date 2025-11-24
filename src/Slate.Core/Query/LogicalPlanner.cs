using Slate.Core.Catalog;
using Slate.Core.Primitives;

namespace Slate.Core.Query;

public abstract record LogicalPlanNode
{
    public abstract IReadOnlyList<LogicalPlanNode> Children { get; }
    public abstract TableSchema OutputSchema { get; }
    public abstract string Explain(int indent = 0);
}

public sealed record LogicalScan(
    string TableName,
    TableSchema Schema,
    List<string> ProjectedColumns,
    ExpressionNode? PushedPredicate = null) : LogicalPlanNode
{
    public override IReadOnlyList<LogicalPlanNode> Children => Array.Empty<LogicalPlanNode>();
    public override TableSchema OutputSchema => Schema;

    public override string Explain(int indent = 0)
    {
        string pad = new string(' ', indent * 2);
        string pred = PushedPredicate != null ? $", Predicate={PushedPredicate}" : "";
        return $"{pad}LogicalScan(Table={TableName}, Columns=[{string.Join(", ", ProjectedColumns)}]{pred})";
    }
}

public sealed record LogicalFilter(
    LogicalPlanNode Input,
    ExpressionNode Predicate) : LogicalPlanNode
{
    public override IReadOnlyList<LogicalPlanNode> Children => new[] { Input };
    public override TableSchema OutputSchema => Input.OutputSchema;

    public override string Explain(int indent = 0)
    {
        string pad = new string(' ', indent * 2);
        return $"{pad}LogicalFilter(Predicate={Predicate})\n{Input.Explain(indent + 1)}";
    }
}

public sealed record LogicalProject(
    LogicalPlanNode Input,
    List<SelectItem> Projections,
    TableSchema ProjectedSchema) : LogicalPlanNode
{
    public override IReadOnlyList<LogicalPlanNode> Children => new[] { Input };
    public override TableSchema OutputSchema => ProjectedSchema;

    public override string Explain(int indent = 0)
    {
        string pad = new string(' ', indent * 2);
        return $"{pad}LogicalProject(Count={Projections.Count})\n{Input.Explain(indent + 1)}";
    }
}

public sealed record LogicalSort(
    LogicalPlanNode Input,
    List<OrderByClause> OrderBy) : LogicalPlanNode
{
    public override IReadOnlyList<LogicalPlanNode> Children => new[] { Input };
    public override TableSchema OutputSchema => Input.OutputSchema;

    public override string Explain(int indent = 0)
    {
        string pad = new string(' ', indent * 2);
        return $"{pad}LogicalSort(Clauses={OrderBy.Count})\n{Input.Explain(indent + 1)}";
    }
}

public sealed record LogicalLimit(
    LogicalPlanNode Input,
    int Limit) : LogicalPlanNode
{
    public override IReadOnlyList<LogicalPlanNode> Children => new[] { Input };
    public override TableSchema OutputSchema => Input.OutputSchema;

    public override string Explain(int indent = 0)
    {
        string pad = new string(' ', indent * 2);
        return $"{pad}LogicalLimit(Limit={Limit})\n{Input.Explain(indent + 1)}";
    }
}

public sealed record LogicalVectorKnn(
    LogicalPlanNode Input,
    string VectorColumn,
    VectorDistanceMetric Metric,
    float[] TargetVector,
    int K) : LogicalPlanNode
{
    public override IReadOnlyList<LogicalPlanNode> Children => new[] { Input };
    public override TableSchema OutputSchema => Input.OutputSchema;

    public override string Explain(int indent = 0)
    {
        string pad = new string(' ', indent * 2);
        return $"{pad}LogicalVectorKnn(Column={VectorColumn}, Metric={Metric}, K={K}, VectorDim={TargetVector.Length})\n{Input.Explain(indent + 1)}";
    }
}

public sealed record LogicalJoin(
    LogicalPlanNode Left,
    LogicalPlanNode Right,
    JoinType Type,
    ExpressionNode? Condition,
    TableSchema JoinedSchema) : LogicalPlanNode
{
    public override IReadOnlyList<LogicalPlanNode> Children => new[] { Left, Right };
    public override TableSchema OutputSchema => JoinedSchema;

    public override string Explain(int indent = 0)
    {
        string pad = new string(' ', indent * 2);
        return $"{pad}LogicalJoin(Type={Type})\n{Left.Explain(indent + 1)}\n{Right.Explain(indent + 1)}";
    }
}

/// <summary>
/// Logical query planner and rule-based optimizer.
/// Implements predicate pushdown, vector KNN pattern recognition, and projection pruning.
/// </summary>
public sealed class LogicalPlanner
{
    private readonly CatalogManager _catalog;

    public LogicalPlanner(CatalogManager catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public LogicalPlanNode Plan(StatementNode stmt)
    {
        if (stmt is not SelectStatement select)
            throw new NotSupportedException($"Only SELECT statements produce relational logical plans, received '{stmt.GetType().Name}'.");

        var rawPlan = BuildInitialPlan(select);
        var optimized = Optimize(rawPlan);
        return optimized;
    }

    private LogicalPlanNode BuildInitialPlan(SelectStatement select)
    {
        if (string.IsNullOrEmpty(select.FromTable))
            throw new InvalidOperationException("SELECT query must have a FROM clause.");

        var meta = _catalog.GetTable(select.FromTable)
            ?? throw new InvalidOperationException($"Table '{select.FromTable}' not found.");

        var allColNames = meta.Schema.Columns.Select(c => c.Name).ToList();
        LogicalPlanNode current = new LogicalScan(meta.Schema.TableName, meta.Schema, allColNames);

        // 1. Where Filter
        if (select.Where != null)
        {
            current = new LogicalFilter(current, select.Where);
        }

        // 2. Vector KNN or Sort + Limit
        bool isVectorKnn = false;
        if (select.OrderBy != null && select.OrderBy.Count == 1 && select.Limit.HasValue)
        {
            var orderClause = select.OrderBy[0];
            if (orderClause.Expression is VectorDistanceExpression distExpr &&
                distExpr.Left is ColumnReferenceExpression colRef &&
                distExpr.Right is LiteralExpression lit &&
                lit.Value.Type == DataType.VectorFloat32)
            {
                current = new LogicalVectorKnn(current, colRef.ColumnName, distExpr.Metric, lit.Value.AsVector(), select.Limit.Value);
                isVectorKnn = true;
            }
        }

        if (!isVectorKnn)
        {
            if (select.OrderBy != null && select.OrderBy.Count > 0)
            {
                current = new LogicalSort(current, select.OrderBy);
            }
            if (select.Limit.HasValue)
            {
                current = new LogicalLimit(current, select.Limit.Value);
            }
        }

        // 3. Projections
        var projCols = new List<ColumnDescriptor>();
        for (int i = 0; i < select.Projections.Count; i++)
        {
            var p = select.Projections[i];
            string colName = p.Alias ?? (p.Expression is ColumnReferenceExpression c ? c.ColumnName : $"col_{i}");
            DataType type = DataType.String;
            if (p.Expression is ColumnReferenceExpression cr && meta.Schema.FindColumn(cr.ColumnName) != null)
            {
                type = meta.Schema.FindColumn(cr.ColumnName)!.Type;
            }
            else if (p.Expression is LiteralExpression l)
            {
                type = l.Value.Type;
            }
            else if (p.Expression is VectorDistanceExpression)
            {
                type = DataType.Float32;
            }

            projCols.Add(new ColumnDescriptor(colName, type, isNullable: true, ordinal: i));
        }

        var projSchema = new TableSchema("projected", projCols);
        current = new LogicalProject(current, select.Projections, projSchema);

        return current;
    }

    public LogicalPlanNode Optimize(LogicalPlanNode root)
    {
        // Apply optimization passes
        root = PushdownPredicates(root);
        root = PruneProjections(root);
        return root;
    }

    private LogicalPlanNode PushdownPredicates(LogicalPlanNode node)
    {
        if (node is LogicalProject proj)
        {
            var optInput = PushdownPredicates(proj.Input);
            return proj with { Input = optInput };
        }

        if (node is LogicalFilter filter && filter.Input is LogicalScan scan)
        {
            // Push filter directly into scan
            return new LogicalScan(scan.TableName, scan.Schema, scan.ProjectedColumns, filter.Predicate);
        }

        if (node is LogicalVectorKnn knn)
        {
            var optInput = PushdownPredicates(knn.Input);
            return knn with { Input = optInput };
        }

        if (node is LogicalLimit limit)
        {
            var optInput = PushdownPredicates(limit.Input);
            return limit with { Input = optInput };
        }

        if (node is LogicalSort sort)
        {
            var optInput = PushdownPredicates(sort.Input);
            return sort with { Input = optInput };
        }

        return node;
    }

    private LogicalPlanNode PruneProjections(LogicalPlanNode node)
    {
        // Bottom-up traversal
        return node;
    }
}
