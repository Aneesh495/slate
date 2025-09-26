using Slate.Core.Primitives;
using Slate.Core.Storage;
using Xunit;

namespace Slate.Tests;

public sealed class WalTests
{
    [Fact]
    public void WalWriter_AppendAndReplay_RecoversExactTransactionSequence()
    {
        using var ms = new MemoryStream();
        using (var writer = new WalWriter(ms))
        {
            writer.Append(1, WalRecordType.Begin, "orders", SlateValue.Null, Array.Empty<SlateValue>());
            writer.Append(1, WalRecordType.Insert, "orders", SlateValue.FromInt64(1001), new[]
            {
                SlateValue.FromInt64(1001),
                SlateValue.FromString("AAPL"),
                SlateValue.FromFloat64(150.25)
            });
            writer.Append(1, WalRecordType.Commit, "orders", SlateValue.Null, Array.Empty<SlateValue>());
        }

        var records = WalReader.ReadAll(ms, out bool cleanEof);
        Assert.True(cleanEof);
        Assert.Equal(3, records.Count);

        Assert.Equal(1ul, records[0].Lsn);
        Assert.Equal(WalRecordType.Begin, records[0].RecordType);

        Assert.Equal(2ul, records[1].Lsn);
        Assert.Equal(WalRecordType.Insert, records[1].RecordType);
        Assert.Equal("orders", records[1].TableName);
        Assert.Equal(SlateValue.FromInt64(1001), records[1].PrimaryKey);
        Assert.Equal(3, records[1].Values.Length);
        Assert.Equal("AAPL", records[1].Values[1].AsString());

        Assert.Equal(3ul, records[2].Lsn);
        Assert.Equal(WalRecordType.Commit, records[2].RecordType);
    }

    [Fact]
    public void WalReader_CorruptedTail_TruncatesSafely()
    {
        using var ms = new MemoryStream();
        using (var writer = new WalWriter(ms))
        {
            writer.Append(1, WalRecordType.Begin, "test", SlateValue.Null, Array.Empty<SlateValue>());
            writer.Append(1, WalRecordType.Commit, "test", SlateValue.Null, Array.Empty<SlateValue>());
        }

        // Simulate half-written trailing frame from a sudden system crash / power cut
        byte[] junk = new byte[] { 0x42, 0x13, 0x37, 0x00, 0xAA };
        ms.Write(junk);

        var records = WalReader.ReadAll(ms, out bool cleanEof);
        Assert.False(cleanEof);
        Assert.Equal(2, records.Count); // Clean records recovered
    }
}
