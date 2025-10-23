using Slate.Core.Primitives;
using Slate.Core.Transactions;
using Xunit;

namespace Slate.Tests;

public sealed class MvccTests
{
    [Fact]
    public void MvccManager_BasicLifecycle_AllocatesTimestamps()
    {
        var mvcc = new MvccManager(100);
        var tx1 = mvcc.BeginTransaction();
        var tx2 = mvcc.BeginTransaction();

        Assert.True(tx2.TxId > tx1.TxId);
        Assert.Equal(100ul, tx1.ReadTimestamp);
        Assert.Equal(100ul, tx2.ReadTimestamp);

        tx1.RecordWrite("users", SlateValue.FromInt64(1));
        mvcc.CommitTransaction(tx1);

        Assert.Equal(TransactionStatus.Committed, tx1.Status);
        Assert.Equal(101ul, tx1.CommitTimestamp);
    }

    [Fact]
    public void MvccManager_WriteWriteConflict_ThrowsWriteConflictException()
    {
        var mvcc = new MvccManager(200);

        // tx1 and tx2 start at snapshot 200
        var tx1 = mvcc.BeginTransaction();
        var tx2 = mvcc.BeginTransaction();

        // Both write to the same key in table 'orders'
        tx1.RecordWrite("orders", SlateValue.FromInt64(99));
        tx2.RecordWrite("orders", SlateValue.FromInt64(99));

        // tx1 commits first at commit timestamp 201
        mvcc.CommitTransaction(tx1);
        Assert.Equal(TransactionStatus.Committed, tx1.Status);

        // tx2 attempts to commit: should detect that key 99 was updated at 201 > tx2.ReadTimestamp (200)
        var ex = Assert.Throws<WriteConflictException>(() => mvcc.CommitTransaction(tx2));
        Assert.Contains("Write-write conflict", ex.Message);
        Assert.Equal(TransactionStatus.Aborted, tx2.Status);
    }

    [Fact]
    public void MvccManager_SerializableIsolation_DetectsReadWriteConflict()
    {
        var mvcc = new MvccManager(300);

        // tx1 is serializable
        var tx1 = mvcc.BeginTransaction(IsolationLevel.Serializable);
        var tx2 = mvcc.BeginTransaction();

        // tx1 reads balance of account 42
        tx1.RecordRead("accounts", SlateValue.FromInt64(42));

        // tx2 modifies and commits account 42
        tx2.RecordWrite("accounts", SlateValue.FromInt64(42));
        mvcc.CommitTransaction(tx2);

        // tx1 attempts to commit: should detect anti-dependency violation
        var ex = Assert.Throws<WriteConflictException>(() => mvcc.CommitTransaction(tx1));
        Assert.Contains("Serialization anomaly", ex.Message);
        Assert.Equal(TransactionStatus.Aborted, tx1.Status);
    }

    [Fact]
    public void MvccManager_VersionStore_RespectsVisibility()
    {
        var mvcc = new MvccManager(400);

        var tx1 = mvcc.BeginTransaction();
        mvcc.RecordVersion("items", SlateValue.FromInt64(5), SlateValue.FromString("Sword"), tx1);

        // tx2 begins before tx1 commits; cannot see uncommitted record
        var tx2 = mvcc.BeginTransaction();
        var valBeforeCommit = mvcc.ReadVersion("items", SlateValue.FromInt64(5), tx2);
        Assert.Null(valBeforeCommit);

        // tx1 commits
        mvcc.CommitTransaction(tx1);

        // tx3 begins after tx1 commits; should see record
        var tx3 = mvcc.BeginTransaction();
        var valAfterCommit = mvcc.ReadVersion("items", SlateValue.FromInt64(5), tx3);
        Assert.NotNull(valAfterCommit);
        Assert.Equal("Sword", valAfterCommit!.Value.AsString());
    }

    [Fact]
    public void MvccConflictResolver_XSharp_ResolvesVisibilityCorrectly()
    {
        Assert.True(MvccConflictResolver.CanCommitWithoutConflict(100, 100));
        Assert.False(MvccConflictResolver.CanCommitWithoutConflict(105, 100));

        Assert.True(MvccConflictResolver.IsRecordVisibleAtSnapshot(50, 0, 75));
        Assert.True(MvccConflictResolver.IsRecordVisibleAtSnapshot(50, 100, 75));
        Assert.False(MvccConflictResolver.IsRecordVisibleAtSnapshot(80, 0, 75));
        Assert.False(MvccConflictResolver.IsRecordVisibleAtSnapshot(50, 60, 75));
    }
}
