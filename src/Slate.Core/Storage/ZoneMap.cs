using Slate.Core.Primitives;

namespace Slate.Core.Storage;

public enum ComparisonOp : byte
{
    Equal = 1,
    NotEqual = 2,
    LessThan = 3,
    LessThanOrEqual = 4,
    GreaterThan = 5,
    GreaterThanOrEqual = 6,
    IsNull = 7,
    IsNotNull = 8
}

/// <summary>
/// Columnar block-level metadata statistics tracking min/max bounds, null counts,
/// and a 256-bit Bloom filter to allow aggressive query predicate pruning without decompressing data pages.
/// </summary>
public sealed class ZoneMap
{
    public SlateValue MinValue { get; private set; }
    public SlateValue MaxValue { get; private set; }
    public uint NullCount { get; private set; }
    public uint TotalRows { get; private set; }
    public ulong BloomFilterMask0 { get; private set; }
    public ulong BloomFilterMask1 { get; private set; }
    public ulong BloomFilterMask2 { get; private set; }
    public ulong BloomFilterMask3 { get; private set; }

    public ZoneMap()
    {
        MinValue = SlateValue.Null;
        MaxValue = SlateValue.Null;
        NullCount = 0;
        TotalRows = 0;
        BloomFilterMask0 = 0;
        BloomFilterMask1 = 0;
        BloomFilterMask2 = 0;
        BloomFilterMask3 = 0;
    }

    public void Observe(SlateValue value)
    {
        TotalRows++;
        if (value.IsNull)
        {
            NullCount++;
            return;
        }

        if (MinValue.IsNull || value.CompareTo(MinValue) < 0)
        {
            MinValue = value;
        }

        if (MaxValue.IsNull || value.CompareTo(MaxValue) > 0)
        {
            MaxValue = value;
        }

        // Add to 256-bit bloom filter
        uint hash1 = (uint)value.GetHashCode();
        uint hash2 = hash1 * 0x9E3779B1u; // Knuth multiplicative hash

        SetBloomBit(hash1 & 255);
        SetBloomBit(hash2 & 255);
    }

    private void SetBloomBit(uint bit)
    {
        int word = (int)(bit >> 6);
        int bitInWord = (int)(bit & 63);
        ulong mask = 1UL << bitInWord;

        switch (word)
        {
            case 0: BloomFilterMask0 |= mask; break;
            case 1: BloomFilterMask1 |= mask; break;
            case 2: BloomFilterMask2 |= mask; break;
            case 3: BloomFilterMask3 |= mask; break;
        }
    }

    public bool BloomMayContain(SlateValue value)
    {
        if (value.IsNull) return NullCount > 0;

        uint hash1 = (uint)value.GetHashCode();
        uint hash2 = hash1 * 0x9E3779B1u;

        return CheckBloomBit(hash1 & 255) && CheckBloomBit(hash2 & 255);
    }

    private bool CheckBloomBit(uint bit)
    {
        int word = (int)(bit >> 6);
        int bitInWord = (int)(bit & 63);
        ulong mask = 1UL << bitInWord;

        return word switch
        {
            0 => (BloomFilterMask0 & mask) != 0,
            1 => (BloomFilterMask1 & mask) != 0,
            2 => (BloomFilterMask2 & mask) != 0,
            3 => (BloomFilterMask3 & mask) != 0,
            _ => false
        };
    }

    /// <summary>
    /// Evaluates whether a query predicate can safely skip reading this entire block.
    /// Returns true if NO rows in this block could possibly satisfy the condition.
    /// </summary>
    public bool CanSkip(ComparisonOp op, SlateValue literal)
    {
        if (TotalRows == 0) return true;

        if (op == ComparisonOp.IsNull)
        {
            return NullCount == 0;
        }

        if (op == ComparisonOp.IsNotNull)
        {
            return NullCount == TotalRows;
        }

        // If block contains exclusively nulls, non-null comparisons cannot match
        if (NullCount == TotalRows)
        {
            return true;
        }

        if (literal.IsNull)
        {
            return false;
        }

        switch (op)
        {
            case ComparisonOp.Equal:
                // Check min/max bounds
                if (!MinValue.IsNull && literal.CompareTo(MinValue) < 0) return true;
                if (!MaxValue.IsNull && literal.CompareTo(MaxValue) > 0) return true;
                // Check bloom filter
                if (!BloomMayContain(literal)) return true;
                return false;

            case ComparisonOp.GreaterThan:
                // If literal >= MaxValue, no value in block can be strictly greater than literal
                if (!MaxValue.IsNull && literal.CompareTo(MaxValue) >= 0) return true;
                return false;

            case ComparisonOp.GreaterThanOrEqual:
                // If literal > MaxValue, no value in block can be >= literal
                if (!MaxValue.IsNull && literal.CompareTo(MaxValue) > 0) return true;
                return false;

            case ComparisonOp.LessThan:
                // If literal <= MinValue, no value in block can be strictly less than literal
                if (!MinValue.IsNull && literal.CompareTo(MinValue) <= 0) return true;
                return false;

            case ComparisonOp.LessThanOrEqual:
                // If literal < MinValue, no value in block can be <= literal
                if (!MinValue.IsNull && literal.CompareTo(MinValue) < 0) return true;
                return false;

            default:
                return false;
        }
    }
}
