using Slate.Core.Primitives;
using Slate.Core.Udf;
using Xunit;

namespace Slate.Tests;

public sealed class UdfTests
{
    [Fact]
    public void MathKernels_ExponentialMovingAverage_SmoothesValues()
    {
        double[] series = { 10.0, 20.0, 30.0, 40.0, 50.0 };
        var ema = MathematicalKernels.ExponentialMovingAverage(series, alpha: 0.5);

        Assert.Equal(5, ema.Length);
        Assert.Equal(10.0, ema[0]);
        Assert.Equal(15.0, ema[1]); // 0.5*20 + 0.5*10 = 15
        Assert.Equal(22.5, ema[2]); // 0.5*30 + 0.5*15 = 22.5
    }

    [Fact]
    public void MathKernels_PearsonCorrelation_CalculatesLinearAssociation()
    {
        double[] x = { 1.0, 2.0, 3.0, 4.0, 5.0 };
        double[] y = { 2.0, 4.0, 6.0, 8.0, 10.0 }; // Perfect positive correlation

        double r = MathematicalKernels.PearsonCorrelation(x, y);
        Assert.Equal(1.0, r, precision: 5);

        double[] yInv = { 10.0, 8.0, 6.0, 4.0, 2.0 }; // Perfect negative correlation
        double rInv = MathematicalKernels.PearsonCorrelation(x, yInv);
        Assert.Equal(-1.0, rInv, precision: 5);
    }

    [Fact]
    public void MathKernels_VarianceAndQuantile_EvaluatesStatistics()
    {
        double[] vals = { 10.0, 20.0, 30.0, 40.0, 50.0 };
        var (variance, stdDev) = MathematicalKernels.VarianceAndStdDev(vals);
        Assert.Equal(250.0, variance, precision: 2);
        Assert.Equal(15.811, stdDev, precision: 2);

        double median = MathematicalKernels.Quantile(vals, 0.5);
        Assert.Equal(30.0, median, precision: 2);
    }

    [Fact]
    public void MathKernels_SoftmaxAndL2Normalize_ProduceUnitMetrics()
    {
        float[] logits = { 1.0f, 2.0f, 3.0f };
        MathematicalKernels.Softmax(logits);

        float sum = logits[0] + logits[1] + logits[2];
        Assert.Equal(1.0f, sum, precision: 4);
        Assert.True(logits[2] > logits[1] && logits[1] > logits[0]);

        float[] vec = { 3.0f, 4.0f };
        MathematicalKernels.L2Normalize(vec);
        float norm = MathF.Sqrt(vec[0] * vec[0] + vec[1] * vec[1]);
        Assert.Equal(1.0f, norm, precision: 4);
        Assert.Equal(0.6f, vec[0], precision: 4);
        Assert.Equal(0.8f, vec[1], precision: 4);
    }

    [Fact]
    public void UdfRegistry_BuiltinsAndCustomRegistration_InvokesCorrectly()
    {
        var registry = new UdfRegistry();

        Assert.True(registry.TryGet("SIGMOID", out var sigDesc));
        var sigVal = sigDesc!.Invoker(new[] { SlateValue.FromFloat64(0.0) });
        Assert.Equal(0.5, sigVal.AsFloat64(), precision: 3);

        // Register custom UDF: CUBE(x) -> x^3
        registry.Register(new UdfDescriptor("CUBE", DataType.Int64, new[] { DataType.Int64 }, args =>
        {
            long n = args[0].AsInt64();
            return SlateValue.FromInt64(n * n * n);
        }));

        Assert.True(registry.TryGet("CUBE", out var cubeDesc));
        var cubeVal = cubeDesc!.Invoker(new[] { SlateValue.FromInt64(3) });
        Assert.Equal(27L, cubeVal.AsInt64());
    }
}
