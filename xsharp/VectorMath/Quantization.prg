// VectorMath/Quantization.prg
// Native X# implementation of 8-bit Scalar Quantization (SQ8)
// for 4x memory compression of dense vector embeddings.

USING System

BEGIN NAMESPACE Slate.XSharp.VectorMath

    /// <summary>
    /// Scalar Quantizer (SQ8) compressing 32-bit floats to 8-bit unsigned integers.
    /// </summary>
    PUBLIC CLASS NativeScalarQuantizer
        PROTECT _dimension AS INT
        PROTECT _minValues AS REAL4[]
        PROTECT _maxValues AS REAL4[]
        PROTECT _stepSizes AS REAL4[]

        PUBLIC CONSTRUCTOR(dim AS INT)
            _dimension := dim
            _minValues := REAL4[]{dim}
            _maxValues := REAL4[]{dim}
            _stepSizes := REAL4[]{dim}
            RETURN

        PUBLIC METHOD Train(vectors AS REAL4[][]) AS VOID
            FOR LOCAL d := 1 UPTO _dimension
                _minValues[d] := REAL4.MaxValue
                _maxValues[d] := REAL4.MinValue
            NEXT

            FOREACH LOCAL vec IN vectors
                FOR LOCAL d := 1 UPTO _dimension
                    IF vec[d] < _minValues[d]; _minValues[d] := vec[d]; ENDIF
                    IF vec[d] > _maxValues[d]; _maxValues[d] := vec[d]; ENDIF
                NEXT
            NEXT

            FOR LOCAL d := 1 UPTO _dimension
                LOCAL span := _maxValues[d] - _minValues[d] AS REAL4
                _stepSizes[d] := (span > 0.0F) ? span / 255.0F : 1.0F
            NEXT
            RETURN

        PUBLIC METHOD Quantize(vec AS REAL4[]) AS BYTE[]
            LOCAL code := BYTE[]{_dimension} AS BYTE[]
            FOR LOCAL d := 1 UPTO _dimension
                LOCAL v := (vec[d] - _minValues[d]) / _stepSizes[d] AS REAL4
                IF v < 0.0F; v := 0.0F; ENDIF
                IF v > 255.0F; v := 255.0F; ENDIF
                code[d] := (BYTE)v
            NEXT
            RETURN code

        PUBLIC METHOD Dequantize(code AS BYTE[]) AS REAL4[]
            LOCAL vec := REAL4[]{_dimension} AS REAL4[]
            FOR LOCAL d := 1 UPTO _dimension
                vec[d] := _minValues[d] + (REAL4)code[d] * _stepSizes[d]
            NEXT
            RETURN vec
    END CLASS

END NAMESPACE
