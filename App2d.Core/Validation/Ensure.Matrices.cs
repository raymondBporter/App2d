using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Validation;

public static partial class Ensure
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3x2 Number(Matrix3x2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNumber(value) ? value : throw CreateOutOfRange(value, "Value must not be NaN.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3x2 Finite(Matrix3x2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsFinite(value) ? value : throw CreateOutOfRange(value, "Value must be finite.", paramName);

    public static Matrix3x2 Invertible(Matrix3x2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        Invertible(value, out _, paramName);

    public static Matrix3x2 Invertible(Matrix3x2 value, out Matrix3x2 inverse, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsInvertible(value, out inverse)) ThrowOutOfRange(value, "Matrix must have a finite inverse.", paramName);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4x4 Number(Matrix4x4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNumber(value) ? value : throw CreateOutOfRange(value, "Value must not be NaN.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix4x4 Finite(Matrix4x4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsFinite(value) ? value : throw CreateOutOfRange(value, "Value must be finite.", paramName);

    public static Matrix4x4 Invertible(Matrix4x4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        Invertible(value, out _, paramName);

    public static Matrix4x4 Invertible(Matrix4x4 value, out Matrix4x4 inverse, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsInvertible(value, out inverse)) ThrowOutOfRange(value, "Matrix must have a finite inverse.", paramName);
        return value;
    }

}
