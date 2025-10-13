using Slate.Core.Indexing;
using Xunit;

namespace Slate.Tests;

public sealed class BPlusTreeTests
{
    [Fact]
    public void BPlusTree_InsertAndSearch_MaintainsBalancedTree()
    {
        var tree = new BPlusTree<long, string>(maxDegree: 4);

        for (long i = 1; i <= 100; i++)
        {
            tree.Insert(i, $"Value_{i}");
        }

        Assert.Equal(100, tree.Count);

        for (long i = 1; i <= 100; i++)
        {
            bool found = tree.TrySearch(i, out string? val);
            Assert.True(found);
            Assert.Equal($"Value_{i}", val);
        }

        Assert.False(tree.TrySearch(999, out _));
    }

    [Fact]
    public void BPlusTree_RangeScan_ReturnsOrderedSubsequence()
    {
        var tree = new BPlusTree<long, int>(maxDegree: 6);

        for (long i = 10; i <= 100; i += 10)
        {
            tree.Insert(i, (int)i);
        }

        // Range scan [25, 75] -> expects 30, 40, 50, 60, 70
        var range = tree.RangeScan(25, 75);
        Assert.Equal(5, range.Count);
        Assert.Equal(30L, range[0].Key);
        Assert.Equal(70L, range[4].Key);
    }
}
