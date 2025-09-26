using Slate.Core.Primitives;

namespace Slate.Core.Storage;

public enum WalRecordType : byte
{
    Invalid = 0,
    Begin = 1,
    Commit = 2,
    Abort = 3,
    Insert = 4,
    Update = 5,
    Delete = 6,
    Checkpoint = 7
}

/// <summary>
/// A single transactional log entry in the Slate Write-Ahead Log (WAL).
/// </summary>
public sealed class WalRecord
{
    public ulong Lsn { get; }
    public ulong TxId { get; }
    public WalRecordType RecordType { get; }
    public string TableName { get; }
    public SlateValue PrimaryKey { get; }
    public SlateValue[] Values { get; }

    public WalRecord(
        ulong lsn,
        ulong txId,
        WalRecordType recordType,
        string tableName,
        SlateValue primaryKey,
        SlateValue[] values)
    {
        Lsn = lsn;
        TxId = txId;
        RecordType = recordType;
        TableName = tableName ?? string.Empty;
        PrimaryKey = primaryKey;
        Values = values ?? Array.Empty<SlateValue>();
    }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream();
        using var writer = new BinaryWriter(ms);

        writer.Write(Lsn);
        writer.Write(TxId);
        writer.Write((byte)RecordType);
        writer.Write(TableName);

        // Primary key
        writer.Write((byte)PrimaryKey.Type);
        writer.Write(PrimaryKey.AsString());

        // Value array
        writer.Write(Values.Length);
        for (int i = 0; i < Values.Length; i++)
        {
            var v = Values[i];
            writer.Write((byte)v.Type);
            writer.Write(v.IsNull);
            if (!v.IsNull)
            {
                switch (v.Type)
                {
                    case DataType.Boolean:
                        writer.Write(v.AsBoolean());
                        break;
                    case DataType.Int8:
                    case DataType.Int16:
                    case DataType.Int32:
                    case DataType.Int64:
                    case DataType.TimestampUtc:
                        writer.Write(v.AsInt64());
                        break;
                    case DataType.Float32:
                    case DataType.Float64:
                        writer.Write(v.AsFloat64());
                        break;
                    case DataType.String:
                        writer.Write(v.AsString());
                        break;
                    case DataType.VectorFloat32:
                        float[] vec = v.AsVector();
                        writer.Write(vec.Length);
                        for (int d = 0; d < vec.Length; d++) writer.Write(vec[d]);
                        break;
                    default:
                        writer.Write(v.AsString());
                        break;
                }
            }
        }

        return ms.ToArray();
    }

    public static WalRecord Deserialize(ReadOnlySpan<byte> span)
    {
        using var ms = new MemoryStream(span.ToArray());
        using var reader = new BinaryReader(ms);

        ulong lsn = reader.ReadUInt64();
        ulong txId = reader.ReadUInt64();
        var recordType = (WalRecordType)reader.ReadByte();
        string tableName = reader.ReadString();

        var pkType = (DataType)reader.ReadByte();
        string pkStr = reader.ReadString();
        var pk = ParseScalar(pkType, pkStr);

        int valueCount = reader.ReadInt32();
        var values = new SlateValue[valueCount];

        for (int i = 0; i < valueCount; i++)
        {
            var valType = (DataType)reader.ReadByte();
            bool isNull = reader.ReadBoolean();
            if (isNull)
            {
                values[i] = SlateValue.Null;
            }
            else
            {
                values[i] = valType switch
                {
                    DataType.Boolean => SlateValue.FromBoolean(reader.ReadBoolean()),
                    DataType.Int8 => SlateValue.FromInt8((sbyte)reader.ReadInt64()),
                    DataType.Int16 => SlateValue.FromInt16((short)reader.ReadInt64()),
                    DataType.Int32 => SlateValue.FromInt32((int)reader.ReadInt64()),
                    DataType.Int64 => SlateValue.FromInt64(reader.ReadInt64()),
                    DataType.TimestampUtc => SlateValue.FromTimestampUtc(reader.ReadInt64()),
                    DataType.Float32 => SlateValue.FromFloat32((float)reader.ReadDouble()),
                    DataType.Float64 => SlateValue.FromFloat64(reader.ReadDouble()),
                    DataType.String => SlateValue.FromString(reader.ReadString()),
                    DataType.VectorFloat32 => ReadVector(reader),
                    _ => SlateValue.FromString(reader.ReadString())
                };
            }
        }

        return new WalRecord(lsn, txId, recordType, tableName, pk, values);
    }

    private static SlateValue ReadVector(BinaryReader reader)
    {
        int dim = reader.ReadInt32();
        var arr = new float[dim];
        for (int d = 0; d < dim; d++) arr[d] = reader.ReadSingle();
        return SlateValue.FromVector(arr);
    }

    private static SlateValue ParseScalar(DataType type, string text) => type switch
    {
        DataType.Int8 or DataType.Int16 or DataType.Int32 or DataType.Int64 => SlateValue.FromInt64(long.Parse(text)),
        DataType.Float32 or DataType.Float64 => SlateValue.FromFloat64(double.Parse(text)),
        DataType.Boolean => SlateValue.FromBoolean(bool.Parse(text)),
        DataType.TimestampUtc => SlateValue.FromTimestampUtc(long.Parse(text)),
        _ => SlateValue.FromString(text)
    };
}
