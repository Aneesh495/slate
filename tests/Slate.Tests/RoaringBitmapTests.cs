using Slate.Core.Indexing;
using Xunit;

namespace Slate.Tests;

public sealed class RoaringBitmapTests
{
    [Fact]
    public void RoaringBitmap_SparseAndDense_MaintainsCorrectCardinality()
    {
        var bitmap = new RoaringBitmap();

        // Add 100 sparse values
        for (ulong i = 0; i < 100; i++)
        {
            bitmap.Add(i * 1000);
        }

        Assert.Equal(100, bitmap.Cardinality);
        Assert.True(bitmap.Contains(5000));
        Assert.False(bitmap.Contains(5001));

        // Add 5000 consecutive values to trigger conversion to BitsetContainer
        for (ulong i = 100_000; i < 105_000; i++)
        {
            bitmap.Add(i);
        }

        Assert.Equal(5100, bitmap.Cardinality);
        Assert.True(bitmap.Contains(102_500));
        Assert.False(bitmap.Contains(106_000));
    }

    [Fact]
    public void RoaringBitmap_AndAndOr_ExecutesAccurateSetAlgebra()
    {
        var b1 = new RoaringBitmap();
        var b2 = new RoaringBitmap();

        // b1: multiples of 2 up to 100
        for (ulong i = 0; i <= 100; i += 2) b1.Add(i);

        // b2: multiples of 3 up to 100
        for (ulong i = 0; i <= 100; i += 3) b2.Add(i);

        // Intersection: multiples of 6 up to 100
        var intersection = b1.And(b2);
        ulong[] actualIntersection = intersection.ToArray();

        var expectedIntersection = Enumerable.Range(0, 101)
            .Where(x => x % 6 == 0)
            .Select(x => (ulong)x)
            .ToArray();

        Assert.Equal(expectedIntersection, actualIntersection);

        // Union: multiples of 2 OR 3
        var union = b1.Or(b2);
        Assert.Equal(expectedIntersection.Length + (b1.Cardinality - expectedIntersection.Length) + (b2.Cardinality - expectedIntersection.Length), union.Cardinality);
    }
}
