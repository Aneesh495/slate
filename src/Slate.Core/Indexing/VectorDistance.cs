using System.Numerics;

namespace Slate.Core.Indexing;

public enum VectorMetric : byte
{
    Cosine = 1,
    Euclidean = 2,
    DotProduct = 3
}

/// <summary>
/// High-throughput vectorized SIMD distance functions for high-dimensional floating-point embeddings.
/// Utilizes hardware Vector<float> 128-bit/256-bit SIMD registers where available.
/// </summary>
public static class VectorDistance
{
    public static float ComputeDistance(ReadOnlySpan<float> a, ReadOnlySpan<float> b, VectorMetric metric) => metric switch
    {
        VectorMetric.Cosine => CosineDistance(a, b),
        VectorMetric.Euclidean => EuclideanDistanceSquared(a, b),
        VectorMetric.DotProduct => -DotProduct(a, b),
        _ => EuclideanDistanceSquared(a, b)
    };

    public static float DotProduct(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vector dimensions must match.", nameof(b));

        int simdBits = Vector<float>.Count;
        int i = 0;
        var sumVector = Vector<float>.Zero;

        for (; i <= a.Length - simdBits; i += simdBits)
        {
            var va = new Vector<float>(a.Slice(i, simdBits));
            var vb = new Vector<float>(b.Slice(i, simdBits));
            sumVector += va * vb;
        }

        float sum = Vector.Dot(sumVector, Vector<float>.One);

        // Remainder loop
        for (; i < a.Length; i++)
        {
            sum += a[i] * b[i];
        }

        return sum;
    }

    public static float EuclideanDistanceSquared(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vector dimensions must match.", nameof(b));

        int simdBits = Vector<float>.Count;
        int i = 0;
        var sumVector = Vector<float>.Zero;

        for (; i <= a.Length - simdBits; i += simdBits)
        {
            var va = new Vector<float>(a.Slice(i, simdBits));
            var vb = new Vector<float>(b.Slice(i, simdBits));
            var diff = va - vb;
            sumVector += diff * diff;
        }

        float sum = Vector.Dot(sumVector, Vector<float>.One);

        for (; i < a.Length; i++)
        {
            float diff = a[i] - b[i];
            sum += diff * diff;
        }

        return sum;
    }

    public static float CosineDistance(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Vector dimensions must match.", nameof(b));

        float dot = 0f;
        float normA = 0f;
        float normB = 0f;

        int simdBits = Vector<float>.Count;
        int i = 0;
        var dotVec = Vector<float>.Zero;
        var normAVec = Vector<float>.Zero;
        var normBVec = Vector<float>.Zero;

        for (; i <= a.Length - simdBits; i += simdBits)
        {
            var va = new Vector<float>(a.Slice(i, simdBits));
            var vb = new Vector<float>(b.Slice(i, simdBits));
            dotVec += va * vb;
            normAVec += va * va;
            normBVec += vb * vb;
        }

        dot += Vector.Dot(dotVec, Vector<float>.One);
        normA += Vector.Dot(normAVec, Vector<float>.One);
        normB += Vector.Dot(normBVec, Vector<float>.One);

        for (; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA <= 0f || normB <= 0f) return 1f;

        float cosineSimilarity = dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
        return 1f - Math.Clamp(cosineSimilarity, -1f, 1f);
    }
}
