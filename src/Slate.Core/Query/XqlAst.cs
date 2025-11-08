using Slate.Core.Catalog;
using Slate.Core.Primitives;

namespace Slate.Core.Query;

public enum BinaryOp
{
    Add,
    Subtract,
    Multiply,
    Divide,
    Equal,
    NotEqual,
    LessThan,
    LessThanOrEqual,
    GreaterThan,
    GreaterThanOrEqual,
    And,
    Or
}

public enum VectorDistanceMetric
{
    L2,
    DotProduct,
    Cosine
}

public enum UnaryOp
{
    Negate,
    Not,
    IsNull,
    IsNotNull
}

public abstract record ExpressionNode;

public sealed record LiteralExpression(SlateValue Value) : ExpressionNode;

public sealed record ColumnReferenceExpression(string? TableName, string ColumnName) : ExpressionNode;

public sealed record BinaryExpression(ExpressionNode Left, BinaryOp Op, ExpressionNode Right) : ExpressionNode;

public sealed record VectorDistanceExpression(ExpressionNode Left, VectorDistanceMetric Metric, ExpressionNode Right) : ExpressionNode;

public sealed record UnaryExpression(UnaryOp Op, ExpressionNode Operand) : ExpressionNode;

public sealed record FunctionCallExpression(string FunctionName, List<ExpressionNode> Arguments) : ExpressionNode;

public sealed record SelectItem(ExpressionNode Expression, string? Alias = null);

public enum JoinType
{
    Inner,
    Left,
    Right,
    Cross
}

public sealed record JoinClause(JoinType Type, string TableName, string? Alias, ExpressionNode? OnCondition);

public sealed record OrderByClause(ExpressionNode Expression, bool Ascending = true);

public sealed record AssignmentClause(string ColumnName, ExpressionNode Value);

public sealed record ColumnDefinition(
    string Name,
    DataType Type,
    bool IsNullable = true,
    int VectorDimension = 0,
    bool IsPrimaryKey = false);

public abstract record StatementNode;

public sealed record SelectStatement(
    List<SelectItem> Projections,
    string? FromTable,
    string? FromAlias = null,
    List<JoinClause>? Joins = null,
    ExpressionNode? Where = null,
    List<ExpressionNode>? GroupBy = null,
    List<OrderByClause>? OrderBy = null,
    int? Limit = null) : StatementNode;

public sealed record InsertStatement(
    string TableName,
    List<string>? ColumnNames,
    List<List<ExpressionNode>> Rows) : StatementNode;

public sealed record UpdateStatement(
    string TableName,
    List<AssignmentClause> Assignments,
    ExpressionNode? Where = null) : StatementNode;

public sealed record DeleteStatement(
    string TableName,
    ExpressionNode? Where = null) : StatementNode;

public sealed record CreateTableStatement(
    string TableName,
    List<ColumnDefinition> Columns,
    string PrimaryKeyColumn) : StatementNode;

public sealed record CreateIndexStatement(
    string IndexName,
    string TableName,
    IndexType IndexType,
    List<string> ColumnNames,
    Dictionary<string, string>? Properties = null) : StatementNode;

public sealed record DropTableStatement(string TableName) : StatementNode;

public enum TransactionAction
{
    Begin,
    Commit,
    Abort
}

public sealed record TransactionStatement(TransactionAction Action) : StatementNode;

public sealed record ExplainStatement(StatementNode TargetStatement) : StatementNode;
