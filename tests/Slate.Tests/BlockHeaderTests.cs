using System.Runtime.InteropServices;
using Slate.Core.Storage;
using Xunit;

namespace Slate.Tests;

public sealed class BlockHeaderTests
{
    [Fact]
    public void BlockHeader_StructSize_IsExactly64Bytes()
    {
        Assert.Equal(64, Marshal.SizeOf<BlockHeader>());
    }

    [Fact]
    public void BlockHeader_SerializationRoundtrip_PreservesAllFields()
    {
        var original = BlockHeader.Create(
            type: BlockType.ColumnDataChunk,
            codec: CompressionCodec.Gorilla,
            uncompressedBytes: 1024,
            compressedBytes: 256,
            rowCount: 100,
            crc32c: 0xDEADBEEF,
            lsn: 42
        );

        byte[] bytes = original.ToByteArray();
        Assert.Equal(64, bytes.Length);

        var restored = BlockHeader.FromByteArray(bytes);
        Assert.True(restored.IsValid());
        Assert.Equal(BlockType.ColumnDataChunk, restored.Type);
        Assert.Equal(CompressionCodec.Gorilla, restored.Codec);
        Assert.Equal(1024u, restored.UncompressedBytes);
        Assert.Equal(256u, restored.CompressedBytes);
        Assert.Equal(100u, restored.RowCount);
        Assert.Equal(0xDEADBEEFu, restored.ChecksumCrc32C);
        Assert.Equal(42ul, restored.Lsn);
    }
}
