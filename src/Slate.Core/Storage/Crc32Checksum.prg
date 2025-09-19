// Storage/Crc32Checksum.prg
// Castagnoli CRC32C polynomial routines and verification in native X#

USING System
USING System.Text

BEGIN NAMESPACE Slate.Core.Storage

    /// <summary>
    /// CRC32C test vector validation and rolling checksum utilities in native X#.
    /// </summary>
    PUBLIC CLASS Crc32Verifier
        PUBLIC CONST StandardNistInput := "123456789" AS STRING
        PUBLIC CONST ExpectedNistCrc := 0xE3069283 AS DWORD

        PUBLIC STATIC METHOD VerifyKnownVector() AS LOGIC
            LOCAL bytes := Encoding.ASCII:GetBytes(StandardNistInput) AS BYTE[]
            LOCAL computed := Crc32C.Compute(bytes) AS DWORD
            RETURN computed == ExpectedNistCrc

        PUBLIC STATIC METHOD ComputeStringChecksum(input AS STRING) AS DWORD
            IF String.IsNullOrEmpty(input)
                RETURN 0
            ENDIF
            LOCAL bytes := Encoding.UTF8:GetBytes(input) AS BYTE[]
            RETURN Crc32C.Compute(bytes)
    END CLASS

END NAMESPACE
