namespace Slate.Core.Udf;

/// <summary>
/// High-performance mathematical, statistical, and neural kernels for in-database analytics.
/// Designed for zero-allocation in-place execution over columnar vectors.
/// </summary>
public static class MathematicalKernels
{
    public static double[] ExponentialMovingAverage(ReadOnlySpan<double> values, double alpha)
    {
        if (alpha <= 0.0 || alpha > 1.0)
            throw new ArgumentOutOfRangeException(nameof(alpha), "Alpha must be in range (0.0, 1.0].");

        if (values.Length == 0)
            return Array.Empty<double>();

        var result = new double[values.Length];
        result[0] = values[0];

        for (int i = 1; i < values.Length; i++)
        {
            result[i] = alpha * values[i] + (1.0 - alpha) * result[i - 1];
        }

        return result;
    }

    public static double PearsonCorrelation(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        if (x.Length != y.Length)
            throw new ArgumentException("Vectors must have identical length.");
        if (x.Length < 2)
            return 0.0;

        int n = x.Length;
        double sumX = 0.0, sumY = 0.0;
        for (int i = 0; i < n; i++)
        {
            sumX += x[i];
            sumY += y[i];
        }

        double meanX = sumX / n;
        double meanY = sumY / n;

        double cov = 0.0, varX = 0.0, varY = 0.0;
        for (int i = 0; i < n; i++)
        {
            double dx = x[i] - meanX;
            double dy = y[i] - meanY;
            cov += dx * dy;
            varX += dx * dx;
            varY += dy * dy;
        }

        double denom = Math.Sqrt(varX * varY);
        return denom == 0.0 ? 0.0 : cov / denom;
    }

    public static (double Variance, double StdDev) VarianceAndStdDev(ReadOnlySpan<double> values)
    {
        if (values.Length < 2)
            return (0.0, 0.0);

        int n = values.Length;
        double sum = 0.0;
        for (int i = 0; i < n; i++) sum += values[i];
        double mean = sum / n;

        double sumSqDiff = 0.0;
        for (int i = 0; i < n; i++)
        {
            double diff = values[i] - mean;
            sumSqDiff += diff * diff;
        }

        double variance = sumSqDiff / (n - 1);
        return (variance, Math.Sqrt(variance));
    }

    public static double Quantile(ReadOnlySpan<double> values, double q)
    {
        if (q < 0.0 || q > 1.0)
            throw new ArgumentOutOfRangeException(nameof(q), "Quantile q must be between 0.0 and 1.0.");
        if (values.Length == 0)
            return 0.0;
        if (values.Length == 1)
            return values[0];

        var sorted = values.ToArray();
        Array.Sort(sorted);

        double pos = q * (sorted.Length - 1);
        int index = (int)pos;
        double frac = pos - index;

        if (index + 1 < sorted.Length)
        {
            return sorted[index] + frac * (sorted[index + 1] - sorted[index]);
        }
        return sorted[index];
    }

    public static double ShannonEntropy(ReadOnlySpan<double> probabilities)
    {
        double entropy = 0.0;
        for (int i = 0; i < probabilities.Length; i++)
        {
            double p = probabilities[i];
            if (p > 0.0)
            {
                entropy -= p * Math.Log2(p);
            }
        }
        return entropy;
    }

    public static void Softmax(Span<float> logits)
    {
        if (logits.Length == 0) return;

        // Find max for numerical stability
        float max = logits[0];
        for (int i = 1; i < logits.Length; i++)
        {
            if (logits[i] > max) max = logits[i];
        }

        float sum = 0.0f;
        for (int i = 0; i < logits.Length; i++)
        {
            logits[i] = MathF.Exp(logits[i] - max);
            sum += logits[i];
        }

        if (sum > 0.0f)
        {
            float invSum = 1.0f / sum;
            for (int i = 0; i < logits.Length; i++)
            {
                logits[i] *= invSum;
            }
        }
    }

    public static void Sigmoid(Span<float> values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            values[i] = 1.0f / (1.0f + MathF.Exp(-values[i]));
        }
    }

    public static void L2Normalize(Span<float> vector)
    {
        float sumSq = 0.0f;
        for (int i = 0; i < vector.Length; i++)
        {
            sumSq += vector[i] * vector[i];
        }

        float norm = MathF.Sqrt(sumSq);
        if (norm > 0.0f)
        {
            float invNorm = 1.0f / norm;
            for (int i = 0; i < vector.Length; i++)
            {
                vector[i] *= invNorm;
            }
        }
    }
}
