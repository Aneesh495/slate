// Primitives/Types.prg
// Core logical data types and scalar properties for Slate HTAP engine

USING System
USING System.Collections.Generic

BEGIN NAMESPACE Slate.Core.Primitives

    /// <summary>
    /// Logical scalar and composite types supported across Slate columnar pages.
    /// </summary>
    PUBLIC ENUM SlateDataType AS BYTE
        MEMBER @@Null          := 0
        MEMBER @@Boolean       := 1
        MEMBER @@Int8          := 2
        MEMBER @@Int16         := 3
        MEMBER @@Int32         := 4
        MEMBER @@Int64         := 5
        MEMBER @@Float32       := 6
        MEMBER @@Float64       := 7
        MEMBER @@String        := 8
        MEMBER @@TimestampUtc  := 9
        MEMBER @@Decimal128    := 10
        MEMBER @@Binary        := 11
        MEMBER @@VectorFloat32 := 12
    END ENUM

    /// <summary>
    /// Type descriptor utilities implemented in native X#.
    /// </summary>
    PUBLIC STATIC CLASS TypeMetrics
        PUBLIC STATIC METHOD GetByteWidth(t AS SlateDataType) AS INT
            DO CASE
            CASE t == SlateDataType.Boolean .OR. t == SlateDataType.Int8
                RETURN 1
            CASE t == SlateDataType.Int16
                RETURN 2
            CASE t == SlateDataType.Int32 .OR. t == SlateDataType.Float32
                RETURN 4
            CASE t == SlateDataType.Int64 .OR. t == SlateDataType.Float64 .OR. t == SlateDataType.TimestampUtc
                RETURN 8
            CASE t == SlateDataType.Decimal128
                RETURN 16
            OTHERWISE
                RETURN -1
            ENDCASE

        PUBLIC STATIC METHOD IsFixedLength(t AS SlateDataType) AS LOGIC
            RETURN GetByteWidth(t) > 0

        PUBLIC STATIC METHOD IsNumeric(t AS SlateDataType) AS LOGIC
            DO CASE
            CASE t == SlateDataType.Int8 .OR. t == SlateDataType.Int16 .OR. t == SlateDataType.Int32
                RETURN TRUE
            CASE t == SlateDataType.Int64 .OR. t == SlateDataType.Float32 .OR. t == SlateDataType.Float64
                RETURN TRUE
            CASE t == SlateDataType.Decimal128
                RETURN TRUE
            OTHERWISE
                RETURN FALSE
            ENDCASE
    END CLASS

END NAMESPACE
