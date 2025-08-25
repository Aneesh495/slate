using System.Globalization;
using System.Runtime.InteropServices;

namespace Slate.Core.Primitives;

/// <summary>
/// A zero-allocation (for primitives) discriminated union representing any scalar or vector value
/// within Slate memory pages, expression evaluation stacks, and transaction buffers.
/// </summary>
[StructLayout(LayoutKind.Explicit)]
public readonly struct SlateValue : IEquatable<SlateValue>, IComparable<SlateValue>
{
    [FieldOffset(0)]
    private readonly DataType _type;

    [FieldOffset(1)]
    private readonly bool _hasValue;

    [FieldOffset(8)]
    private readonly long _int64Val;

    [FieldOffset(8)]
    private readonly double _float64Val;

    [FieldOffset(16)]
    private readonly object? _objVal;

    public DataType Type => _type;
    public bool IsNull => !_hasValue;
    public bool HasValue => _hasValue;

    public static readonly SlateValue Null = new(DataType.Null, false, 0, null);

    private SlateValue(DataType type, bool hasValue, long intVal, object? objVal)
    {
        _float64Val = 0.0;
        _type = type;
        _hasValue = hasValue;
        _int64Val = intVal;
        _objVal = objVal;
    }

    private SlateValue(DataType type, bool hasValue, double floatVal, object? objVal)
    {
        _int64Val = 0;
        _type = type;
        _hasValue = hasValue;
        _float64Val = floatVal;
        _objVal = objVal;
    }

    public static SlateValue FromBoolean(bool value) => new(DataType.Boolean, true, value ? 1L : 0L, null);
    public static SlateValue FromInt8(sbyte value) => new(DataType.Int8, true, value, null);
    public static SlateValue FromInt16(short value) => new(DataType.Int16, true, value, null);
    public static SlateValue FromInt32(int value) => new(DataType.Int32, true, value, null);
    public static SlateValue FromInt64(long value) => new(DataType.Int64, true, value, null);
    public static SlateValue FromFloat32(float value) => new(DataType.Float32, true, (double)value, null);
    public static SlateValue FromFloat64(double value) => new(DataType.Float64, true, value, null);
    public static SlateValue FromTimestampUtc(long microsecondsSinceEpoch) => new(DataType.TimestampUtc, true, microsecondsSinceEpoch, null);
    public static SlateValue FromString(string? value) => value is null ? Null : new(DataType.String, true, 0L, value);
    public static SlateValue FromBinary(byte[]? value) => value is null ? Null : new(DataType.Binary, true, 0L, value);
    public static SlateValue FromVector(float[]? value) => value is null ? Null : new(DataType.VectorFloat32, true, value.Length, value);

    public bool AsBoolean() => _type == DataType.Boolean ? _int64Val != 0 : throw new InvalidCastException($"Cannot cast {_type} to Boolean.");
    public sbyte AsInt8() => _type == DataType.Int8 ? (sbyte)_int64Val : (sbyte)AsInt64();
    public short AsInt16() => _type == DataType.Int16 ? (short)_int64Val : (short)AsInt64();
    public int AsInt32() => _type == DataType.Int32 ? (int)_int64Val : (int)AsInt64();
    public long AsInt64() => _type switch
    {
        DataType.Int8 or DataType.Int16 or DataType.Int32 or DataType.Int64 or DataType.TimestampUtc => _int64Val,
        DataType.Float32 or DataType.Float64 => (long)_float64Val,
        _ => throw new InvalidCastException($"Cannot cast {_type} to Int64.")
    };

    public float AsFloat32() => _type == DataType.Float32 ? (float)_float64Val : (float)AsFloat64();
    public double AsFloat64() => _type switch
    {
        DataType.Float32 or DataType.Float64 => _float64Val,
        DataType.Int8 or DataType.Int16 or DataType.Int32 or DataType.Int64 => _int64Val,
        _ => throw new InvalidCastException($"Cannot cast {_type} to Float64.")
    };

    public string AsString() => _type switch
    {
        DataType.String => (string)(_objVal ?? string.Empty),
        DataType.Null => "NULL",
        _ => ToString()
    };

    public byte[] AsBinary() => _objVal as byte[] ?? Array.Empty<byte>();
    public float[] AsVector() => _objVal as float[] ?? Array.Empty<float>();

    public bool Equals(SlateValue other)
    {
        if (!_hasValue && !other._hasValue) return true;
        if (!_hasValue || !other._hasValue) return false;
        if (_type != other._type)
        {
            if (_type.IsNumeric() && other._type.IsNumeric())
            {
                return Math.Abs(AsFloat64() - other.AsFloat64()) < 1e-12;
            }
            return false;
        }

        return _type switch
        {
            DataType.Boolean or DataType.Int8 or DataType.Int16 or DataType.Int32 or DataType.Int64 or DataType.TimestampUtc => _int64Val == other._int64Val,
            DataType.Float32 or DataType.Float64 => Math.Abs(_float64Val - other._float64Val) < 1e-12,
            DataType.String => string.Equals((string?)_objVal, (string?)other._objVal, StringComparison.Ordinal),
            DataType.VectorFloat32 => VectorEquals((float[]?)_objVal, (float[]?)other._objVal),
            DataType.Binary => BinaryEquals((byte[]?)_objVal, (byte[]?)other._objVal),
            _ => true
        };
    }

    private static bool VectorEquals(float[]? a, float[]? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (Math.Abs(a[i] - b[i]) > 1e-6f) return false;
        }
        return true;
    }

    private static bool BinaryEquals(byte[]? a, byte[]? b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null || a.Length != b.Length) return false;
        return a.AsSpan().SequenceEqual(b.AsSpan());
    }

    public int CompareTo(SlateValue other)
    {
        if (!_hasValue && !other._hasValue) return 0;
        if (!_hasValue) return -1;
        if (!other._hasValue) return 1;

        if (_type.IsNumeric() && other._type.IsNumeric())
        {
            return AsFloat64().CompareTo(other.AsFloat64());
        }

        if (_type != other._type)
        {
            return _type.CompareTo(other._type);
        }

        return _type switch
        {
            DataType.Boolean or DataType.Int8 or DataType.Int16 or DataType.Int32 or DataType.Int64 or DataType.TimestampUtc => _int64Val.CompareTo(other._int64Val),
            DataType.Float32 or DataType.Float64 => _float64Val.CompareTo(other._float64Val),
            DataType.String => string.Compare((string?)_objVal, (string?)other._objVal, StringComparison.Ordinal),
            _ => 0
        };
    }

    public override bool Equals(object? obj) => obj is SlateValue other && Equals(other);

    public override int GetHashCode()
    {
        if (!_hasValue) return 0;
        return _type switch
        {
            DataType.Boolean or DataType.Int8 or DataType.Int16 or DataType.Int32 or DataType.Int64 or DataType.TimestampUtc => HashCode.Combine((byte)_type, _int64Val),
            DataType.Float32 or DataType.Float64 => HashCode.Combine((byte)_type, _float64Val),
            DataType.String => HashCode.Combine((byte)_type, (string?)_objVal),
            _ => HashCode.Combine((byte)_type, _objVal)
        };
    }

    public override string ToString()
    {
        if (!_hasValue) return "NULL";
        return _type switch
        {
            DataType.Boolean => (_int64Val != 0).ToString(),
            DataType.Int8 or DataType.Int16 or DataType.Int32 or DataType.Int64 => _int64Val.ToString(CultureInfo.InvariantCulture),
            DataType.Float32 or DataType.Float64 => _float64Val.ToString("G17", CultureInfo.InvariantCulture),
            DataType.TimestampUtc => DateTimeOffset.FromUnixTimeMilliseconds(_int64Val / 1000).ToString("o", CultureInfo.InvariantCulture),
            DataType.String => (string)(_objVal ?? string.Empty),
            DataType.VectorFloat32 => $"[Vector Dim={((float[]?)_objVal)?.Length ?? 0}]",
            DataType.Binary => $"<Binary len={((byte[]?)_objVal)?.Length ?? 0}>",
            _ => "NULL"
        };
    }

    public static bool operator ==(SlateValue left, SlateValue right) => left.Equals(right);
    public static bool operator !=(SlateValue left, SlateValue right) => !left.Equals(right);
    public static bool operator <(SlateValue left, SlateValue right) => left.CompareTo(right) < 0;
    public static bool operator <=(SlateValue left, SlateValue right) => left.CompareTo(right) <= 0;
    public static bool operator >(SlateValue left, SlateValue right) => left.CompareTo(right) > 0;
    public static bool operator >=(SlateValue left, SlateValue right) => left.CompareTo(right) >= 0;
}
