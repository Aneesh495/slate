using Slate.Core.Primitives;
using Slate.Core.Storage;
using Xunit;

namespace Slate.Tests;

public sealed class MemTableTests
{
    [Fact]
    public void MemTable_PutAndGet_RespectsCommitTimestamps()
    {
        var mt = new MemTable();
        var key = SlateValue.FromInt64(1);

        // Version 1 at t=10
        mt.Put(key, new[] { key, SlateValue.FromString("V1") }, txId: 1, commitTimestamp: 10);
        // Version 2 at t=20
        mt.Put(key, new[] { key, SlateValue.FromString("V2") }, txId: 2, commitTimestamp: 20);

        // Snapshot at t=15 should see V1
        var e15 = mt.Get(key, readTimestamp: 15);
        Assert.NotNull(e15);
        Assert.Equal("V1", e15.Values[1].AsString());

        // Snapshot at t=25 should see V2
        var e25 = mt.Get(key, readTimestamp: 25);
        Assert.NotNull(e25);
        Assert.Equal("V2", e25.Values[1].AsString());
    }

    [Fact]
    public void MemTable_DeleteTombstone_HidesRecordAfterDeletionTimestamp()
    {
        var mt = new MemTable();
        var key = SlateValue.FromInt64(99);

        mt.Put(key, new[] { key, SlateValue.FromString("Active") }, txId: 1, commitTimestamp: 10);
        mt.Delete(key, txId: 2, commitTimestamp: 30);

        // Before delete (t=20): visible
        var before = mt.Get(key, readTimestamp: 20);
        Assert.NotNull(before);

        // After delete (t=35): invisible (tombstone)
        var after = mt.Get(key, readTimestamp: 35);
        Assert.Null(after);
    }

    [Fact]
    public void MemTable_FlushToVectorBatch_ConvertsRowFormatToColumnar()
    {
        var schema = new TableSchema("products", new[]
        {
            new ColumnDescriptor("id", DataType.Int64, isNullable: false),
            new ColumnDescriptor("title", DataType.String, isNullable: false),
            new ColumnDescriptor("price", DataType.Float64, isNullable: false)
        }, primaryKeyName: "id");

        var mt = new MemTable();
        for (int i = 1; i <= 50; i++)
        {
            var pk = SlateValue.FromInt64(i);
            mt.Put(pk, new[]
            {
                pk,
                SlateValue.FromString($"Product_{i}"),
                SlateValue.FromFloat64(9.99 * i)
            }, txId: 1, commitTimestamp: 100);
        }

        var batch = mt.FlushToVectorBatch(schema, snapshotTimestamp: 100);
        Assert.Equal(50, batch.RowCount);
        Assert.Equal(3, batch.Columns.Length);

        // Verify column 0 (id)
        Assert.Equal(1L, batch[0].GetInt64(0));
        Assert.Equal(50L, batch[0].GetInt64(49));

        // Verify column 1 (title)
        Assert.Equal("Product_1", batch[1].GetString(0));
        Assert.Equal("Product_50", batch[1].GetString(49));
    }
}
