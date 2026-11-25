using App2d.Core.Mathematics.Transforms;
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

public static partial class NumericValidation
{
    public static bool IsNumber(Matrix3x2 value) =>
        IsNumber(value.M11) && IsNumber(value.M12) &&
        IsNumber(value.M21) && IsNumber(value.M22) &&
        IsNumber(value.M31) && IsNumber(value.M32);
    public static bool IsFinite(Matrix3x2 value) => value.IsFinite();
    public static bool IsInvertible(Matrix3x2 value) => IsInvertible(value, out _);
    public static bool IsInvertible(Matrix3x2 value, out Matrix3x2 inverse)
    {
        inverse = default;
        return IsFinite(value) && Matrix3x2.Invert(value, out inverse) && IsFinite(inverse);
    }

    public static bool IsNumber(Matrix4x4 value) =>
        IsNumber(value.M11) && IsNumber(value.M12) && IsNumber(value.M13) && IsNumber(value.M14) &&
        IsNumber(value.M21) && IsNumber(value.M22) && IsNumber(value.M23) && IsNumber(value.M24) &&
        IsNumber(value.M31) && IsNumber(value.M32) && IsNumber(value.M33) && IsNumber(value.M34) &&
        IsNumber(value.M41) && IsNumber(value.M42) && IsNumber(value.M43) && IsNumber(value.M44);
    public static bool IsFinite(Matrix4x4 value) => value.IsFinite();
    public static bool IsInvertible(Matrix4x4 value) => IsInvertible(value, out _);
    public static bool IsInvertible(Matrix4x4 value, out Matrix4x4 inverse)
    {
        inverse = default;
        return IsFinite(value) && Matrix4x4.Invert(value, out inverse) && IsFinite(inverse);
    }
}
