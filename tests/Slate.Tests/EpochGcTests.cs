using Slate.Core.Primitives;
using Slate.Core.Transactions;
using Xunit;

namespace Slate.Tests;

public sealed class EpochGcTests
{
    [Fact]
    public void EpochGc_ComputeSafeWatermark_FindsLowestActiveSnapshot()
    {
        var tx1 = new TransactionContext(1, readTimestamp: 100);
        var tx2 = new TransactionContext(2, readTimestamp: 150);
        var tx3 = new TransactionContext(3, readTimestamp: 80);

        var active = new List<TransactionContext> { tx1, tx2, tx3 };
        ulong watermark = EpochGc.ComputeSafeWatermark(active, currentTimestamp: 200);

        Assert.Equal(80ul, watermark);

        // When no active transactions exist, current timestamp is the safe watermark
        ulong emptyWatermark = EpochGc.ComputeSafeWatermark(Array.Empty<TransactionContext>(), currentTimestamp: 250);
        Assert.Equal(250ul, emptyWatermark);
    }

    [Fact]
    public void EpochGc_Collect_PurgesOnlyEligibleVersions()
    {
        var gc = new EpochGc();
        int reclaimedCount = 0;

        var key1 = new TableKey("trades", SlateValue.FromInt64(1001));
        var key2 = new TableKey("trades", SlateValue.FromInt64(1002));
        var key3 = new TableKey("trades", SlateValue.FromInt64(1003));

        gc.Retire(key1, expiredTimestamp: 50, () => Interlocked.Increment(ref reclaimedCount));
        gc.Retire(key2, expiredTimestamp: 80, () => Interlocked.Increment(ref reclaimedCount));
        gc.Retire(key3, expiredTimestamp: 120, () => Interlocked.Increment(ref reclaimedCount));

        Assert.Equal(3, gc.PendingRetirementsCount);

        // Watermark is 75: only key1 (expired at 50) is eligible
        int collected = gc.Collect(safeWatermark: 75);
        Assert.Equal(1, collected);
        Assert.Equal(1, reclaimedCount);
        Assert.Equal(2, gc.PendingRetirementsCount);

        // Watermark advances to 150: both key2 (80) and key3 (120) are eligible
        int collected2 = gc.Collect(safeWatermark: 150);
        Assert.Equal(2, collected2);
        Assert.Equal(3, reclaimedCount);
        Assert.Equal(0, gc.PendingRetirementsCount);
    }
}
