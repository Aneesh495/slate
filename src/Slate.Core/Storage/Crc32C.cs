namespace Slate.Core.Storage;

/// <summary>
/// Hardware-efficient CRC32C (Castagnoli, polynomial 0x82F63B78) implementation
/// used for block checksums, WAL record validation, and segment integrity verification.
/// </summary>
public static class Crc32C
{
    private const uint Polynomial = 0x82F63B78u;
    private static readonly uint[] Table = new uint[256];

    static Crc32C()
    {
        for (uint i = 0; i < 256; i++)
        {
            uint crc = i;
            for (int j = 0; j < 8; j++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ Polynomial : crc >> 1;
            }
            Table[i] = crc;
        }
    }

    public static uint Compute(ReadOnlySpan<byte> data) => Append(0, data);

    public static uint Append(uint crc, ReadOnlySpan<byte> data)
    {
        uint current = ~crc;
        for (int i = 0; i < data.Length; i++)
        {
            byte index = (byte)(current ^ data[i]);
            current = (current >> 8) ^ Table[index];
        }
        return ~current;
    }
}
