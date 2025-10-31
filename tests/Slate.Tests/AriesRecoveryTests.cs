using Slate.Core.Primitives;
using Slate.Core.Storage;
using Slate.Core.Transactions;
using Xunit;

namespace Slate.Tests;

public sealed class AriesRecoveryTests
{
    [Fact]
    public void AriesRecovery_RecoversWinnersAndRollsBackLosers()
    {
        var schema = new TableSchema("users", new[]
        {
            new ColumnDescriptor("id", DataType.Int64, isNullable: false, ordinal: 0),
            new ColumnDescriptor("name", DataType.String, isNullable: false, ordinal: 1)
        });

        using var walStream = new MemoryStream();
        var walWriter = new WalWriter(walStream);

        // Tx 1: Begin, Insert (id=1, "Alice"), Commit
        walWriter.Append(1, WalRecordType.Begin, "users", SlateValue.Null, Array.Empty<SlateValue>());
        walWriter.Append(1, WalRecordType.Insert, "users", SlateValue.FromInt64(1), new[] { SlateValue.FromInt64(1), SlateValue.FromString("Alice") });
        walWriter.Append(1, WalRecordType.Commit, "users", SlateValue.Null, Array.Empty<SlateValue>());

        // Tx 2: Begin, Insert (id=2, "Bob") -> CRASH before commit!
        walWriter.Append(2, WalRecordType.Begin, "users", SlateValue.Null, Array.Empty<SlateValue>());
        walWriter.Append(2, WalRecordType.Insert, "users", SlateValue.FromInt64(2), new[] { SlateValue.FromInt64(2), SlateValue.FromString("Bob") });

        // Tx 3: Begin, Insert (id=3, "Charlie"), Commit
        walWriter.Append(3, WalRecordType.Begin, "users", SlateValue.Null, Array.Empty<SlateValue>());
        walWriter.Append(3, WalRecordType.Insert, "users", SlateValue.FromInt64(3), new[] { SlateValue.FromInt64(3), SlateValue.FromString("Charlie") });
        walWriter.Append(3, WalRecordType.Commit, "users", SlateValue.Null, Array.Empty<SlateValue>());

        walWriter.Flush();

        // Target memory structures to recover into
        var memTable = new MemTable();
        var mvcc = new MvccManager();

        // Run recovery
        var result = AriesRecoveryEngine.Recover(walStream, memTable, mvcc);

        // Verification of winners and losers
        Assert.Contains(1ul, result.WinnerTransactions);
        Assert.Contains(3ul, result.WinnerTransactions);
        Assert.Contains(2ul, result.LoserTransactions);

        Assert.Equal(3, result.RedoneRecordsCount);
        Assert.Equal(1, result.UndoneRecordsCount); // Tx 2 was undone

        // Verify MemTable state
        // Alice (1) should exist
        var aliceRow = memTable.Get(SlateValue.FromInt64(1), readTimestamp: ulong.MaxValue);
        Assert.NotNull(aliceRow);
        Assert.Equal("Alice", aliceRow!.Values[1].AsString());

        // Charlie (3) should exist
        var charlieRow = memTable.Get(SlateValue.FromInt64(3), readTimestamp: ulong.MaxValue);
        Assert.NotNull(charlieRow);
        Assert.Equal("Charlie", charlieRow!.Values[1].AsString());

        // Bob (2) should NOT exist because Tx 2 was a loser
        var bobRow = memTable.Get(SlateValue.FromInt64(2), readTimestamp: ulong.MaxValue);
        Assert.Null(bobRow);
    }
}
