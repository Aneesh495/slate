using Slate.Core.Primitives;
using Slate.Core.Transactions;
using Xunit;

namespace Slate.Tests;

public sealed class TransactionContextTests
{
    [Fact]
    public void TransactionContext_Lifecycle_TracksWritesAndTransitionsStatus()
    {
        var tx = new TransactionContext(txId: 101, readTimestamp: 500);

        Assert.Equal(101ul, tx.TxId);
        Assert.Equal(500ul, tx.ReadTimestamp);
        Assert.Equal(TransactionStatus.Active, tx.Status);

        tx.RecordWrite("accounts", SlateValue.FromInt64(42));
        Assert.Single(tx.WriteSet);

        tx.MarkCommitted(commitTimestamp: 550);
        Assert.Equal(TransactionStatus.Committed, tx.Status);
        Assert.Equal(550ul, tx.CommitTimestamp);

        // Attempting to record write after commit throws
        Assert.Throws<InvalidOperationException>(() => tx.RecordWrite("accounts", SlateValue.FromInt64(43)));
    }
}
