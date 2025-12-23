using Slate.Engine.Storage;
using Xunit;

namespace Slate.Tests;

public sealed class BufferPoolTests
{
    [Fact]
    public void BufferPoolManager_FetchAndUnpin_TracksHitsAndMisses()
    {
        using var bpm = new BufferPoolManager(poolSize: 4, pageSize: 512);

        // Fetch page 1 -> miss
        var p1 = bpm.FetchPage(1);
        Assert.NotNull(p1);
        Assert.Equal(1ul, p1.PageId);
        Assert.Equal(1, p1.PinCount);
        Assert.Equal(1, bpm.BufferMisses);
        Assert.Equal(0, bpm.BufferHits);

        bpm.UnpinPage(1);
        Assert.Equal(0, p1.PinCount);

        // Fetch page 1 again -> hit
        var p1Hit = bpm.FetchPage(1);
        Assert.NotNull(p1Hit);
        Assert.Equal(1, bpm.BufferHits);
        Assert.Equal(1, bpm.BufferMisses);
        bpm.UnpinPage(1);
    }

    [Fact]
    public void BufferPoolManager_ClockEviction_EvictsUnpinnedFrames()
    {
        var writtenPages = new Dictionary<ulong, byte[]>();
        using var bpm = new BufferPoolManager(poolSize: 2, pageSize: 64);
        bpm.DiskPageWriter = (id, data) => writtenPages[id] = (byte[])data.Clone();

        // Fetch pages 1 and 2
        var p1 = bpm.FetchPage(1);
        p1!.Data[0] = 0xAA;
        bpm.UnpinPage(1, isDirty: true);

        var p2 = bpm.FetchPage(2);
        bpm.UnpinPage(2, isDirty: false);

        // Fetch page 3 -> capacity (2) exceeded, p1 or p2 should be evicted
        var p3 = bpm.FetchPage(3);
        Assert.NotNull(p3);
        Assert.Equal(1, bpm.EvictionCount);

        // Page 1 was dirty, verify it was written to disk
        Assert.True(writtenPages.ContainsKey(1));
        Assert.Equal(0xAA, writtenPages[1][0]);
        bpm.UnpinPage(3);
    }

    [Fact]
    public void BufferPoolManager_PinnedPages_AreProtectedFromEviction()
    {
        using var bpm = new BufferPoolManager(poolSize: 2, pageSize: 64);

        // Pin both frames without unpinning
        var p1 = bpm.FetchPage(1);
        var p2 = bpm.FetchPage(2);

        // Attempting to fetch page 3 when all frames are pinned returns null
        var p3 = bpm.FetchPage(3);
        Assert.Null(p3);

        // Unpin page 1
        bpm.UnpinPage(1);

        // Now page 3 can be fetched by evicting page 1
        p3 = bpm.FetchPage(3);
        Assert.NotNull(p3);
        Assert.Equal(3ul, p3.PageId);

        bpm.UnpinPage(2);
        bpm.UnpinPage(3);
    }
}
