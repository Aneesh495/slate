using Slate.Core.Primitives;
using Slate.Core.Storage;
using Xunit;

namespace Slate.Tests;

public sealed class ColumnChunkTests
{
    [Fact]
    public void ColumnChunkBuilder_Int64Vector_CompressesAndDecompressesLosslessly()
    {
        var vec = new ColumnVector(DataType.Int64, 100);
        for (int i = 0; i < 100; i++)
        {
            vec.AppendInt64(5000 + i);
        }

        var chunk = ColumnChunkBuilder.Build(0, vec);
        Assert.Equal(0, chunk.ColumnOrdinal);
        Assert.Equal(DataType.Int64, chunk.Type);
        Assert.Equal(100, chunk.RowCount);
        Assert.True(chunk.ChecksumCrc32C != 0);

        var restored = chunk.Decompress();
        Assert.Equal(100, restored.Length);
        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(5000 + i, restored.GetInt64(i));
        }
    }

    [Fact]
    public void ColumnChunkBuilder_StringVector_SelectsDictionaryAndRestores()
    {
        var vec = new ColumnVector(DataType.String, 60);
        string[] tags = { "NYSE", "NASDAQ", "LSE", "JPX" };
        for (int i = 0; i < 60; i++)
        {
            vec.AppendString(tags[i % tags.Length]);
        }

        var chunk = ColumnChunkBuilder.Build(1, vec);
        Assert.Equal(CompressionCodec.Dictionary, chunk.Codec);

        var restored = chunk.Decompress();
        Assert.Equal(60, restored.Length);
        for (int i = 0; i < 60; i++)
        {
            Assert.Equal(tags[i % tags.Length], restored.GetString(i));
        }
    }

    [Fact]
    public void ColumnChunkBuilder_DenseVectorFloat32_PreservesEmbeddingCoordinates()
    {
        var vec = new ColumnVector(DataType.VectorFloat32, 5);
        for (int i = 0; i < 5; i++)
        {
            vec.AppendVector(new float[] { i * 0.1f, i * 0.2f, i * 0.3f, 0.42f });
        }

        var chunk = ColumnChunkBuilder.Build(2, vec);
        Assert.Equal(CompressionCodec.None, chunk.Codec);

        var restored = chunk.Decompress();
        Assert.Equal(5, restored.Length);
        for (int i = 0; i < 5; i++)
        {
            float[]? coords = restored.GetVector(i);
            Assert.NotNull(coords);
            Assert.Equal(4, coords.Length);
            Assert.Equal(i * 0.1f, coords[0], precision: 5);
            Assert.Equal(0.42f, coords[3], precision: 5);
        }
    }
}
