using Slate.Core.Primitives;
using Slate.Core.Query;
using Slate.Engine.Compilation;
using Xunit;

namespace Slate.Tests;

public sealed class CompilerTests
{
    [Fact]
    public void DynamicExpressionCompiler_CompilesAndEvaluatesPredicate()
    {
        var schema = new TableSchema("items", new[]
        {
            new ColumnDescriptor("id", DataType.Int64, isNullable: false, ordinal: 0),
            new ColumnDescriptor("score", DataType.Int64, isNullable: false, ordinal: 1)
        });

        var colId = new ColumnVector(DataType.Int64, 4);
        var colScore = new ColumnVector(DataType.Int64, 4);

        colId.AppendInt64(10); colScore.AppendInt64(100);
        colId.AppendInt64(50); colScore.AppendInt64(250);
        colId.AppendInt64(80); colScore.AppendInt64(600);
        colId.AppendInt64(90); colScore.AppendInt64(300);

        var batch = new VectorBatch(schema, new[] { colId, colScore }, 4);

        // Predicate: id >= 50 AND score < 500
        var pred = new BinaryExpression(
            new BinaryExpression(new ColumnReferenceExpression(null, "id"), BinaryOp.GreaterThanOrEqual, new LiteralExpression(SlateValue.FromInt64(50))),
            BinaryOp.And,
            new BinaryExpression(new ColumnReferenceExpression(null, "score"), BinaryOp.LessThan, new LiteralExpression(SlateValue.FromInt64(500)))
        );

        var compiledFunc = DynamicExpressionCompiler.CompilePredicate(pred, schema);
        Assert.NotNull(compiledFunc);

        // Row 0: id=10, score=100 -> False (id < 50)
        Assert.False(compiledFunc(batch, 0));

        // Row 1: id=50, score=250 -> True (50 >= 50 and 250 < 500)
        Assert.True(compiledFunc(batch, 1));

        // Row 2: id=80, score=600 -> False (score not < 500)
        Assert.False(compiledFunc(batch, 2));

        // Row 3: id=90, score=300 -> True
        Assert.True(compiledFunc(batch, 3));
    }
}
