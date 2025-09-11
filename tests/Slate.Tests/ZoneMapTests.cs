using Slate.Core.Primitives;
using Slate.Core.Storage;
using Xunit;

namespace Slate.Tests;

public sealed class ZoneMapTests
{
    [Fact]
    public void ZoneMap_RangeObservation_CalculatesCorrectBounds()
    {
        var zm = new ZoneMap();
        for (int i = 100; i <= 200; i++)
        {
            zm.Observe(SlateValue.FromInt64(i));
        }
        zm.Observe(SlateValue.Null);

        Assert.Equal(102u, zm.TotalRows);
        Assert.Equal(1u, zm.NullCount);
        Assert.Equal(SlateValue.FromInt64(100), zm.MinValue);
        Assert.Equal(SlateValue.FromInt64(200), zm.MaxValue);
    }

    [Fact]
    public void ZoneMap_CanSkip_PrunesIrrelevantPredicates()
    {
        var zm = new ZoneMap();
        for (int i = 100; i <= 200; i++)
        {
            zm.Observe(SlateValue.FromInt64(i));
        }

        // Query: WHERE id = 50 -> 50 < 100, should skip!
        Assert.True(zm.CanSkip(ComparisonOp.Equal, SlateValue.FromInt64(50)));

        // Query: WHERE id = 150 -> 100 <= 150 <= 200, cannot skip
        Assert.False(zm.CanSkip(ComparisonOp.Equal, SlateValue.FromInt64(150)));

        // Query: WHERE id > 250 -> 250 >= MaxValue(200), should skip!
        Assert.True(zm.CanSkip(ComparisonOp.GreaterThan, SlateValue.FromInt64(250)));

        // Query: WHERE id < 50 -> 50 <= MinValue(100), should skip!
        Assert.True(zm.CanSkip(ComparisonOp.LessThan, SlateValue.FromInt64(50)));

        // Query: WHERE id IS NULL -> block has 0 nulls, should skip!
        Assert.True(zm.CanSkip(ComparisonOp.IsNull, SlateValue.Null));

        // Query: WHERE id IS NOT NULL -> block has non-nulls, cannot skip
        Assert.False(zm.CanSkip(ComparisonOp.IsNotNull, SlateValue.Null));
    }
}
