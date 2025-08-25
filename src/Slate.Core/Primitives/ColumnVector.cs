using System.Collections;

namespace Slate.Core.Primitives;

/// <summary>
/// A memory-contiguous columnar vector representing a typed column chunk in vectorized query execution.
/// Avoids boxing of numeric primitives during vectorized filtering, arithmetic, and scans.
/// </summary>
public sealed class ColumnVector
{
    public DataType Type { get; }
    public int Length { get; private set; }
    public int Capacity { get; }

    private readonly long[]? _intBuffer;
    private readonly double[]? _floatBuffer;
    private readonly string?[]? _stringBuffer;
    private readonly float[][]? _vectorBuffer;
    private readonly byte[][]? _binaryBuffer;
    private readonly BitArray _nullBitmap;

    public ColumnVector(DataType type, int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");

        Type = type;
        Capacity = capacity;
        Length = 0;
        _nullBitmap = new BitArray(capacity, false);

        switch (type)
        {
            case DataType.Boolean:
            case DataType.Int8:
            case DataType.Int16:
            case DataType.Int32:
            case DataType.Int64:
            case DataType.TimestampUtc:
                _intBuffer = new long[capacity];
                break;

            case DataType.Float32:
            case DataType.Float64:
                _floatBuffer = new double[capacity];
                break;

            case DataType.String:
                _stringBuffer = new string?[capacity];
                break;

            case DataType.VectorFloat32:
                _vectorBuffer = new float[capacity][];
                break;

            case DataType.Binary:
                _binaryBuffer = new byte[capacity][];
                break;

            default:
                _stringBuffer = new string?[capacity];
                break;
        }
    }

    public bool IsNull(int row) => _nullBitmap[row];

    public void AppendNull()
    {
        EnsureCapacity();
        _nullBitmap[Length] = true;
        Length++;
    }

    public void AppendInt64(long value)
    {
        EnsureCapacity();
        if (_intBuffer is null) throw new InvalidOperationException($"Vector type {Type} does not accept Int64.");
        _intBuffer[Length] = value;
        _nullBitmap[Length] = false;
        Length++;
    }

    public void AppendDouble(double value)
    {
        EnsureCapacity();
        if (_floatBuffer is null) throw new InvalidOperationException($"Vector type {Type} does not accept Double.");
        _floatBuffer[Length] = value;
        _nullBitmap[Length] = false;
        Length++;
    }

    public void AppendString(string? value)
    {
        if (value is null)
        {
            AppendNull();
            return;
        }
        EnsureCapacity();
        if (_stringBuffer is null) throw new InvalidOperationException($"Vector type {Type} does not accept String.");
        _stringBuffer[Length] = value;
        _nullBitmap[Length] = false;
        Length++;
    }

    public void AppendVector(float[]? value)
    {
        if (value is null)
        {
            AppendNull();
            return;
        }
        EnsureCapacity();
        if (_vectorBuffer is null) throw new InvalidOperationException($"Vector type {Type} does not accept Vector.");
        _vectorBuffer[Length] = value;
        _nullBitmap[Length] = false;
        Length++;
    }

    public void AppendValue(SlateValue value)
    {
        if (value.IsNull)
        {
            AppendNull();
            return;
        }

        switch (Type)
        {
            case DataType.Boolean:
            case DataType.Int8:
            case DataType.Int16:
            case DataType.Int32:
            case DataType.Int64:
            case DataType.TimestampUtc:
                AppendInt64(value.AsInt64());
                break;
            case DataType.Float32:
            case DataType.Float64:
                AppendDouble(value.AsFloat64());
                break;
            case DataType.String:
                AppendString(value.AsString());
                break;
            case DataType.VectorFloat32:
                AppendVector(value.AsVector());
                break;
            case DataType.Binary:
                EnsureCapacity();
                _binaryBuffer![Length] = value.AsBinary();
                _nullBitmap[Length] = false;
                Length++;
                break;
            default:
                AppendNull();
                break;
        }
    }

    public long GetInt64(int row)
    {
        CheckBounds(row);
        return _intBuffer![row];
    }

    public double GetDouble(int row)
    {
        CheckBounds(row);
        return _floatBuffer![row];
    }

    public string? GetString(int row)
    {
        CheckBounds(row);
        return _nullBitmap[row] ? null : _stringBuffer![row];
    }

    public float[]? GetVector(int row)
    {
        CheckBounds(row);
        return _nullBitmap[row] ? null : _vectorBuffer![row];
    }

    public SlateValue GetValue(int row)
    {
        CheckBounds(row);
        if (_nullBitmap[row]) return SlateValue.Null;

        return Type switch
        {
            DataType.Boolean => SlateValue.FromBoolean(_intBuffer![row] != 0),
            DataType.Int8 => SlateValue.FromInt8((sbyte)_intBuffer![row]),
            DataType.Int16 => SlateValue.FromInt16((short)_intBuffer![row]),
            DataType.Int32 => SlateValue.FromInt32((int)_intBuffer![row]),
            DataType.Int64 => SlateValue.FromInt64(_intBuffer![row]),
            DataType.TimestampUtc => SlateValue.FromTimestampUtc(_intBuffer![row]),
            DataType.Float32 => SlateValue.FromFloat32((float)_floatBuffer![row]),
            DataType.Float64 => SlateValue.FromFloat64(_floatBuffer![row]),
            DataType.String => SlateValue.FromString(_stringBuffer![row]),
            DataType.VectorFloat32 => SlateValue.FromVector(_vectorBuffer![row]),
            DataType.Binary => SlateValue.FromBinary(_binaryBuffer![row]),
            _ => SlateValue.Null
        };
    }

    private void EnsureCapacity()
    {
        if (Length >= Capacity)
            throw new InvalidOperationException($"ColumnVector has reached maximum capacity of {Capacity}.");
    }

    private void CheckBounds(int row)
    {
        if (row < 0 || row >= Length)
            throw new ArgumentOutOfRangeException(nameof(row), $"Row {row} is outside bounds [0, {Length}).");
    }
}
