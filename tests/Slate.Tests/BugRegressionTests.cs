using System.Runtime.InteropServices;
using Slate.Core.Indexing;
using Slate.Core.Primitives;
using Slate.Core.Storage;
using Slate.Core.Transactions;
using Xunit;

namespace Slate.Tests;

public sealed class BugRegressionTests
{
    [Fact]
    public void Bug001_BlockHeader_SizeOf_MustBeExactly64Bytes()
    {
        int structSize = Marshal.SizeOf<BlockHeader>();
        Assert.Equal(64, structSize);
    }

    [Fact]
    public void Bug002_BitPackedCodec_WordBoundaryExhaustion_PreservesIntegrity()
    {
        // Tests edge case where bit boundaries align across words
        for (int step = 1; step <= 32; step++)
        {
            int count = 256;
            var values = new long[count];
            for (int i = 0; i < count; i++)
            {
                values[i] = (long)i * step;
            }

            byte[] encoded = BitPackedCodec.EncodeInt64(values);
            long[] decoded = BitPackedCodec.DecodeInt64(encoded);

            Assert.Equal(values, decoded);
        }
    }

    [Fact]
    public void Bug003_WalWriter_DoesNotCloseUnderlyingStreamOnDispose()
    {
        var memStream = new MemoryStream();
        using (var writer = new WalWriter(memStream))
        {
            writer.Append(1, WalRecordType.Begin, "t", SlateValue.Null, Array.Empty<SlateValue>());
            writer.Flush();
        }

        // Stream should still be open and readable
        Assert.True(memStream.CanRead);
        Assert.True(memStream.Length > 0);
        memStream.Dispose();
    }

    [Fact]
    public void Bug004_MemTable_ConcurrentReadLockRecursion_DoesNotThrow()
    {
        var schema = new TableSchema("t", new[] { new ColumnDescriptor("id", DataType.Int64, isNullable: false, ordinal: 0) });
        var memTable = new MemTable();

        for (int i = 0; i < 50; i++)
        {
            memTable.Put(SlateValue.FromInt64(i), new[] { SlateValue.FromInt64(i) }, 1, 100);
        }

        // FlushToVectorBatch performs internal read operations
        var batch = memTable.FlushToVectorBatch(schema, snapshotTimestamp: 200);
        Assert.Equal(50, batch.RowCount);
    }

    [Fact]
    public void Bug005_AdaptiveRadixTree_UpdateExistingKey_DoesNotDuplicate()
    {
        var art = new AdaptiveRadixTree();
        art.Insert(System.Text.Encoding.UTF8.GetBytes("alpha"), 100ul);
        art.Insert(System.Text.Encoding.UTF8.GetBytes("alpha"), 200ul); // Overwrite

        Assert.Equal(1, art.Count);
        bool found = art.TrySearch(System.Text.Encoding.UTF8.GetBytes("alpha"), out ulong val);
        Assert.True(found);
        Assert.Equal(200ul, val);
    }

    [Fact]
    public void Bug006_Mvcc_UncommittedVersion_IsInvisibleToOtherTransactions()
    {
        var mvcc = new MvccManager(100);

        var tx1 = mvcc.BeginTransaction();
        mvcc.RecordVersion("users", SlateValue.FromInt64(42), SlateValue.FromString("Secret"), tx1);

        // Concurrent tx2 should not see uncommitted write
        var tx2 = mvcc.BeginTransaction();
        var val = mvcc.ReadVersion("users", SlateValue.FromInt64(42), tx2);
        Assert.Null(val);

        // Once tx1 commits, a subsequent tx3 sees it
        mvcc.CommitTransaction(tx1);
        var tx3 = mvcc.BeginTransaction();
        var valAfter = mvcc.ReadVersion("users", SlateValue.FromInt64(42), tx3);
        Assert.NotNull(valAfter);
        Assert.Equal("Secret", valAfter!.Value.AsString());
    }
}
