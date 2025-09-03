using Slate.Core.Storage;
using Xunit;

namespace Slate.Tests;

public sealed class DictionaryCodecTests
{
    [Fact]
    public void DictionaryCodec_RepetitiveStrings_CompressesAccurately()
    {
        var original = new List<string?>();
        string[] categories = { "electronics", "fashion", "books", "groceries", "automotive" };

        for (int i = 0; i < 500; i++)
        {
            original.Add(categories[i % categories.Length]);
        }
        original.Add(null); // Add null entry

        byte[] compressed = DictionaryCodec.EncodeStrings(original);
        // Uncompressed 500 strings * avg 8 bytes = ~4000 bytes
        // Compressed: dict table (approx 50 bytes) + bit-packed 3-bit indices (501 * 3 / 8 = ~188 bytes) < 350 bytes!
        Assert.True(compressed.Length < 600);

        string?[] decoded = DictionaryCodec.DecodeStrings(compressed);
        Assert.Equal(original, decoded);
    }

    [Fact]
    public void DictionaryCodec_EmptyAndNull_HandledCorrectly()
    {
        var original = new string?[] { null, null, null };
        byte[] compressed = DictionaryCodec.EncodeStrings(original);
        string?[] decoded = DictionaryCodec.DecodeStrings(compressed);

        Assert.Equal(original, decoded);
    }
}
