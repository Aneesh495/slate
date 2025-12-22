using System.Reflection;
using Slate.Core.Primitives;
using Slate.Core.Query;
using LinqExpr = System.Linq.Expressions.Expression;
using LinqParam = System.Linq.Expressions.ParameterExpression;

namespace Slate.Engine.Compilation;

/// <summary>
/// Dynamic expression compiler utilizing System.Linq.Expressions to JIT-compile
/// relational filter predicates into native CIL code.
/// </summary>
public static class DynamicExpressionCompiler
{
    private static readonly MethodInfo GetValueMethod = typeof(VectorBatch).GetMethod(
        nameof(VectorBatch.GetValue),
        new[] { typeof(int), typeof(int) })!;

    private static readonly MethodInfo AsInt64Method = typeof(SlateValue).GetMethod(nameof(SlateValue.AsInt64))!;
    private static readonly MethodInfo AsFloat64Method = typeof(SlateValue).GetMethod(nameof(SlateValue.AsFloat64))!;
    private static readonly MethodInfo AsBooleanMethod = typeof(SlateValue).GetMethod(nameof(SlateValue.AsBoolean))!;
    private static readonly MethodInfo AsStringMethod = typeof(SlateValue).GetMethod(nameof(SlateValue.AsString))!;
    private static readonly MethodInfo CompareToMethod = typeof(SlateValue).GetMethod(nameof(SlateValue.CompareTo), new[] { typeof(SlateValue) })!;

    public static Func<VectorBatch, int, bool> CompilePredicate(ExpressionNode predicate, TableSchema schema)
    {
        var batchParam = LinqExpr.Parameter(typeof(VectorBatch), "batch");
        var rowParam = LinqExpr.Parameter(typeof(int), "row");

        var body = BuildPredicateExpression(predicate, schema, batchParam, rowParam);
        var lambda = LinqExpr.Lambda<Func<VectorBatch, int, bool>>(body, batchParam, rowParam);
        return lambda.Compile();
    }

    private static LinqExpr BuildPredicateExpression(
        ExpressionNode expr,
        TableSchema schema,
        LinqParam batchParam,
        LinqParam rowParam)
    {
        if (expr is Slate.Core.Query.BinaryExpression bin)
        {
            if (bin.Op == BinaryOp.And)
            {
                var l = BuildPredicateExpression(bin.Left, schema, batchParam, rowParam);
                var r = BuildPredicateExpression(bin.Right, schema, batchParam, rowParam);
                return LinqExpr.AndAlso(l, r);
            }
            if (bin.Op == BinaryOp.Or)
            {
                var l = BuildPredicateExpression(bin.Left, schema, batchParam, rowParam);
                var r = BuildPredicateExpression(bin.Right, schema, batchParam, rowParam);
                return LinqExpr.OrElse(l, r);
            }

            var leftVal = BuildValueExpression(bin.Left, schema, batchParam, rowParam);
            var rightVal = BuildValueExpression(bin.Right, schema, batchParam, rowParam);
            var cmp = LinqExpr.Call(leftVal, CompareToMethod, rightVal);
            var zero = LinqExpr.Constant(0);

            return bin.Op switch
            {
                BinaryOp.Equal => LinqExpr.Equal(cmp, zero),
                BinaryOp.NotEqual => LinqExpr.NotEqual(cmp, zero),
                BinaryOp.LessThan => LinqExpr.LessThan(cmp, zero),
                BinaryOp.LessThanOrEqual => LinqExpr.LessThanOrEqual(cmp, zero),
                BinaryOp.GreaterThan => LinqExpr.GreaterThan(cmp, zero),
                BinaryOp.GreaterThanOrEqual => LinqExpr.GreaterThanOrEqual(cmp, zero),
                _ => LinqExpr.Constant(false)
            };
        }

        if (expr is Slate.Core.Query.UnaryExpression un && un.Op == UnaryOp.Not)
        {
            var inner = BuildPredicateExpression(un.Operand, schema, batchParam, rowParam);
            return LinqExpr.Not(inner);
        }

        return LinqExpr.Constant(true);
    }

    private static LinqExpr BuildValueExpression(
        ExpressionNode expr,
        TableSchema schema,
        LinqParam batchParam,
        LinqParam rowParam)
    {
        if (expr is LiteralExpression lit)
        {
            return LinqExpr.Constant(lit.Value, typeof(SlateValue));
        }

        if (expr is ColumnReferenceExpression colRef)
        {
            int ordinal = schema[colRef.ColumnName].Ordinal;
            var ordConst = LinqExpr.Constant(ordinal);
            return LinqExpr.Call(batchParam, GetValueMethod, ordConst, rowParam);
        }

        return LinqExpr.Constant(SlateValue.Null, typeof(SlateValue));
    }
}
