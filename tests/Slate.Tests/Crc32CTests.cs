using System.Text;
using Slate.Core.Storage;
using Xunit;

namespace Slate.Tests;

public sealed class Crc32CTests
{
    [Fact]
    public void Crc32C_StandardVector_MatchesNistCastagnoli()
    {
        // Standard check: "123456789" (ASCII) under Castagnoli polynomial is 0xE3069283
        byte[] input = Encoding.ASCII.GetBytes("123456789");
        uint crc = Crc32C.Compute(input);
        Assert.Equal(0xE3069283u, crc);
    }

    [Fact]
    public void Crc32C_EmptyInput_ReturnsZero()
    {
        Assert.Equal(0u, Crc32C.Compute(ReadOnlySpan<byte>.Empty));
    }

    [Fact]
    public void Crc32C_PiecewiseAppend_ProducesIdenticalResult()
    {
        byte[] data1 = Encoding.UTF8.GetBytes("Slate HTAP Database Engine");
        byte[] data2 = Encoding.UTF8.GetBytes(" - Vector-Relational Lakehouse Kernel");
        byte[] full = Encoding.UTF8.GetBytes("Slate HTAP Database Engine - Vector-Relational Lakehouse Kernel");

        uint fullCrc = Crc32C.Compute(full);

        uint step1 = Crc32C.Compute(data1);
        uint step2 = Crc32C.Append(step1, data2);

        Assert.Equal(fullCrc, step2);
    }
}
