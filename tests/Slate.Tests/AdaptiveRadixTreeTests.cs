using System.Text;
using Slate.Core.Indexing;
using Xunit;

namespace Slate.Tests;

public sealed class AdaptiveRadixTreeTests
{
    [Fact]
    public void ART_InsertAndSearch_HandlesDynamicNodeTransitions()
    {
        var art = new AdaptiveRadixTree();

        // Insert 100 keys with shared prefixes to exercise node upgrades
        for (int i = 0; i < 100; i++)
        {
            byte[] key = Encoding.ASCII.GetBytes($"instrument/stock/US/ticker_{i:D4}");
            art.Insert(key, (ulong)i);
        }

        Assert.Equal(100, art.Count);

        for (int i = 0; i < 100; i++)
        {
            byte[] key = Encoding.ASCII.GetBytes($"instrument/stock/US/ticker_{i:D4}");
            bool found = art.TrySearch(key, out ulong val);
            Assert.True(found);
            Assert.Equal((ulong)i, val);
        }

        byte[] missing = Encoding.ASCII.GetBytes("instrument/stock/US/ticker_9999");
        Assert.False(art.TrySearch(missing, out _));
    }

    [Fact]
    public void ART_PrefixSearch_ReturnsAllMatchingChildren()
    {
        var art = new AdaptiveRadixTree();
        string[] symbols = { "AAPL", "AMZN", "AMD", "BAC", "BABA", "BRK", "GOOG" };

        for (int i = 0; i < symbols.Length; i++)
        {
            art.Insert(Encoding.ASCII.GetBytes(symbols[i]), (ulong)i);
        }

        var aPrefix = art.PrefixSearch(Encoding.ASCII.GetBytes("A"));
        Assert.Equal(3, aPrefix.Count); // AAPL, AMZN, AMD

        var bPrefix = art.PrefixSearch(Encoding.ASCII.GetBytes("B"));
        Assert.Equal(3, bPrefix.Count); // BAC, BABA, BRK

        var gPrefix = art.PrefixSearch(Encoding.ASCII.GetBytes("GOOG"));
        Assert.Single(gPrefix);
    }
}
