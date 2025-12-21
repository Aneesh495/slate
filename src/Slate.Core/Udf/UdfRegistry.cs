using System.Collections.Concurrent;
using Slate.Core.Primitives;

namespace Slate.Core.Udf;

public delegate SlateValue ScalarUdfDelegate(IReadOnlyList<SlateValue> arguments);

public sealed record UdfDescriptor(
    string Name,
    DataType ReturnType,
    IReadOnlyList<DataType> ArgumentTypes,
    ScalarUdfDelegate Invoker);

/// <summary>
/// Central registry for registering and resolving user-defined scalar and vector analytics functions.
/// </summary>
public sealed class UdfRegistry
{
    private readonly ConcurrentDictionary<string, UdfDescriptor> _udfs = new(StringComparer.OrdinalIgnoreCase);

    public UdfRegistry()
    {
        RegisterBuiltins();
    }

    public void Register(UdfDescriptor descriptor)
    {
        if (descriptor == null) throw new ArgumentNullException(nameof(descriptor));
        _udfs[descriptor.Name] = descriptor;
    }

    public bool TryGet(string name, out UdfDescriptor? descriptor) => _udfs.TryGetValue(name, out descriptor);

    public IReadOnlyCollection<string> ListFunctions() => _udfs.Keys.OrderBy(k => k).ToList();

    private void RegisterBuiltins()
    {
        // SIGMOID(float) -> float
        Register(new UdfDescriptor("SIGMOID", DataType.Float64, new[] { DataType.Float64 }, args =>
        {
            if (args.Count != 1 || args[0].IsNull) return SlateValue.Null;
            double v = args[0].AsFloat64();
            return SlateValue.FromFloat64(1.0 / (1.0 + Math.Exp(-v)));
        }));

        // ENTROPY(p1, p2) -> float
        Register(new UdfDescriptor("ENTROPY2", DataType.Float64, new[] { DataType.Float64, DataType.Float64 }, args =>
        {
            if (args.Count != 2 || args[0].IsNull || args[1].IsNull) return SlateValue.Null;
            double p1 = args[0].AsFloat64();
            double p2 = args[1].AsFloat64();
            double ent = 0.0;
            if (p1 > 0.0) ent -= p1 * Math.Log2(p1);
            if (p2 > 0.0) ent -= p2 * Math.Log2(p2);
            return SlateValue.FromFloat64(ent);
        }));

        // L2_NORM(vector) -> vector
        Register(new UdfDescriptor("L2_NORM", DataType.VectorFloat32, new[] { DataType.VectorFloat32 }, args =>
        {
            if (args.Count != 1 || args[0].IsNull) return SlateValue.Null;
            float[] vec = (float[])args[0].AsVector().Clone();
            MathematicalKernels.L2Normalize(vec);
            return SlateValue.FromVector(vec);
        }));
    }
}
