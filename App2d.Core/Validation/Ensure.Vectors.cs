using App2d.Core.Mathematics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Validation;

public static partial class Ensure
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vec2<TReal> Finite<TReal>(Vec2<TReal> value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where TReal : unmanaged, IFloatingPointIeee754<TReal> =>
        NumericValidation.IsFinite(value) ? value : throw CreateOutOfRange(value, "Value must be finite.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Number(Vector2f value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNumber(value) ? value : throw CreateOutOfRange(value, "Value must not be NaN.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Finite(Vector2f value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsFinite(value) ? value : throw CreateOutOfRange(value, "Value must be finite.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Positive(Vector2f value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsPositive(value) ? value : throw CreateOutOfRange(value, "Value must be finite and positive (> 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f Negative(Vector2f value, [CallerArgumentExpression(nameof(value))] string? paramName = null, bool allowNegativeInfinity = false) =>
        NumericValidation.IsNegative(value, allowNegativeInfinity) ? value : throw CreateOutOfRange(value, "Value must be negative (< 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f NonNegative(Vector2f value, [CallerArgumentExpression(nameof(value))] string? paramName = null, bool allowInfinity = false) =>
        NumericValidation.IsNonNegative(value, allowInfinity) ? value : throw CreateOutOfRange(value, "Value must be nonnegative (>= 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f NonPositive(Vector2f value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNonPositive(value) ? value : throw CreateOutOfRange(value, "Value must be finite and nonpositive (<= 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f NonZero(Vector2f value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNonZero(value) ? value : throw CreateOutOfRange(value, "Value must be finite and nonzero.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f LessThan(Vector2f value, Vector2f maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null, bool allowInfinity = false)
    {
        if (allowInfinity) Number(value, paramName); else Finite(value, paramName);
        if (allowInfinity) Number(maximum, maximumParamName); else Finite(maximum, maximumParamName);
        return NumericValidation.IsLessThan(value, maximum) ? value
            : throw CreateOutOfRange(value, $"Value must be < {maximum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f LessThanOrEqual(Vector2f value, Vector2f maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null, bool allowInfinity = false)
    {
        if (allowInfinity) Number(value, paramName); else Finite(value, paramName);
        if (allowInfinity) Number(maximum, maximumParamName); else Finite(maximum, maximumParamName);
        return NumericValidation.IsLessThanOrEqual(value, maximum) ? value
            : throw CreateOutOfRange(value, $"Value must be <= {maximum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f GreaterThan(Vector2f value, Vector2f minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null, bool allowInfinity = false)
    {
        if (allowInfinity) Number(value, paramName); else Finite(value, paramName);
        if (allowInfinity) Number(minimum, minimumParamName); else Finite(minimum, minimumParamName);
        return NumericValidation.IsGreaterThan(value, minimum) ? value
            : throw CreateOutOfRange(value, $"Value must be > {minimum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f GreaterThanOrEqual(Vector2f value, Vector2f minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null, bool allowInfinity = false)
    {
        if (allowInfinity) Number(value, paramName); else Finite(value, paramName);
        if (allowInfinity) Number(minimum, minimumParamName); else Finite(minimum, minimumParamName);
        return NumericValidation.IsGreaterThanOrEqual(value, minimum) ? value
            : throw CreateOutOfRange(value, $"Value must be >= {minimum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2f InRange(Vector2f value, Vector2f minimum, Vector2f maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        RangeBounds bounds = RangeBounds.Closed,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null)
    {
        ValidateRangeBounds(bounds);
        Number(minimum, minimumParamName);
        Number(maximum, maximumParamName);
        if (!(bounds == RangeBounds.Closed
            ? NumericValidation.IsLessThanOrEqual(minimum, maximum)
            : NumericValidation.IsLessThan(minimum, maximum)))
        {
            ThrowOutOfRange(maximum, "Bounds must be ordered and describe a nonempty range on every axis.", maximumParamName);
        }

        return NumericValidation.IsInRange(value, minimum, maximum, bounds)
            ? value : throw CreateRangeException(value, minimum, maximum, bounds, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f Number(Vector3f value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNumber(value) ? value : throw CreateOutOfRange(value, "Value must not be NaN.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f Finite(Vector3f value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsFinite(value) ? value : throw CreateOutOfRange(value, "Value must be finite.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f Positive(Vector3f value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsPositive(value) ? value : throw CreateOutOfRange(value, "Value must be finite and positive (> 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f Negative(Vector3f value, [CallerArgumentExpression(nameof(value))] string? paramName = null, bool allowNegativeInfinity = false) =>
        NumericValidation.IsNegative(value, allowNegativeInfinity) ? value : throw CreateOutOfRange(value, "Value must be negative (< 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f NonNegative(Vector3f value, [CallerArgumentExpression(nameof(value))] string? paramName = null, bool allowInfinity = false) =>
        NumericValidation.IsNonNegative(value, allowInfinity) ? value : throw CreateOutOfRange(value, "Value must be nonnegative (>= 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f NonPositive(Vector3f value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNonPositive(value) ? value : throw CreateOutOfRange(value, "Value must be finite and nonpositive (<= 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f NonZero(Vector3f value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNonZero(value) ? value : throw CreateOutOfRange(value, "Value must be finite and nonzero.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f LessThan(Vector3f value, Vector3f maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null, bool allowInfinity = false)
    {
        if (allowInfinity) Number(value, paramName); else Finite(value, paramName);
        if (allowInfinity) Number(maximum, maximumParamName); else Finite(maximum, maximumParamName);
        return NumericValidation.IsLessThan(value, maximum) ? value
            : throw CreateOutOfRange(value, $"Value must be < {maximum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f LessThanOrEqual(Vector3f value, Vector3f maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null, bool allowInfinity = false)
    {
        if (allowInfinity) Number(value, paramName); else Finite(value, paramName);
        if (allowInfinity) Number(maximum, maximumParamName); else Finite(maximum, maximumParamName);
        return NumericValidation.IsLessThanOrEqual(value, maximum) ? value
            : throw CreateOutOfRange(value, $"Value must be <= {maximum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f GreaterThan(Vector3f value, Vector3f minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null, bool allowInfinity = false)
    {
        if (allowInfinity) Number(value, paramName); else Finite(value, paramName);
        if (allowInfinity) Number(minimum, minimumParamName); else Finite(minimum, minimumParamName);
        return NumericValidation.IsGreaterThan(value, minimum) ? value
            : throw CreateOutOfRange(value, $"Value must be > {minimum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f GreaterThanOrEqual(Vector3f value, Vector3f minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null, bool allowInfinity = false)
    {
        if (allowInfinity) Number(value, paramName); else Finite(value, paramName);
        if (allowInfinity) Number(minimum, minimumParamName); else Finite(minimum, minimumParamName);
        return NumericValidation.IsGreaterThanOrEqual(value, minimum) ? value
            : throw CreateOutOfRange(value, $"Value must be >= {minimum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3f InRange(Vector3f value, Vector3f minimum, Vector3f maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        RangeBounds bounds = RangeBounds.Closed,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null)
    {
        ValidateRangeBounds(bounds);
        Number(minimum, minimumParamName);
        Number(maximum, maximumParamName);
        if (!(bounds == RangeBounds.Closed
            ? NumericValidation.IsLessThanOrEqual(minimum, maximum)
            : NumericValidation.IsLessThan(minimum, maximum)))
        {
            ThrowOutOfRange(maximum, "Bounds must be ordered and describe a nonempty range on every axis.", maximumParamName);
        }

        return NumericValidation.IsInRange(value, minimum, maximum, bounds)
            ? value : throw CreateRangeException(value, minimum, maximum, bounds, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Number(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNumber(value) ? value : throw CreateOutOfRange(value, "Value must not be NaN.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Finite(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsFinite(value) ? value : throw CreateOutOfRange(value, "Value must be finite.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Positive(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsPositive(value) ? value : throw CreateOutOfRange(value, "Value must be finite and positive (> 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Negative(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null, bool allowNegativeInfinity = false) =>
        NumericValidation.IsNegative(value, allowNegativeInfinity) ? value : throw CreateOutOfRange(value, "Value must be negative (< 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 NonNegative(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null, bool allowInfinity = false) =>
        NumericValidation.IsNonNegative(value, allowInfinity) ? value : throw CreateOutOfRange(value, "Value must be nonnegative (>= 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 NonPositive(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNonPositive(value) ? value : throw CreateOutOfRange(value, "Value must be finite and nonpositive (<= 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 NonZero(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNonZero(value) ? value : throw CreateOutOfRange(value, "Value must be finite and nonzero.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 LessThan(Vector4 value, Vector4 maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null, bool allowInfinity = false)
    {
        if (allowInfinity) Number(value, paramName); else Finite(value, paramName);
        if (allowInfinity) Number(maximum, maximumParamName); else Finite(maximum, maximumParamName);
        return NumericValidation.IsLessThan(value, maximum) ? value
            : throw CreateOutOfRange(value, $"Value must be < {maximum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 LessThanOrEqual(Vector4 value, Vector4 maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null, bool allowInfinity = false)
    {
        if (allowInfinity) Number(value, paramName); else Finite(value, paramName);
        if (allowInfinity) Number(maximum, maximumParamName); else Finite(maximum, maximumParamName);
        return NumericValidation.IsLessThanOrEqual(value, maximum) ? value
            : throw CreateOutOfRange(value, $"Value must be <= {maximum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 GreaterThan(Vector4 value, Vector4 minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null, bool allowInfinity = false)
    {
        if (allowInfinity) Number(value, paramName); else Finite(value, paramName);
        if (allowInfinity) Number(minimum, minimumParamName); else Finite(minimum, minimumParamName);
        return NumericValidation.IsGreaterThan(value, minimum) ? value
            : throw CreateOutOfRange(value, $"Value must be > {minimum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 GreaterThanOrEqual(Vector4 value, Vector4 minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null, bool allowInfinity = false)
    {
        if (allowInfinity) Number(value, paramName); else Finite(value, paramName);
        if (allowInfinity) Number(minimum, minimumParamName); else Finite(minimum, minimumParamName);
        return NumericValidation.IsGreaterThanOrEqual(value, minimum) ? value
            : throw CreateOutOfRange(value, $"Value must be >= {minimum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 InRange(Vector4 value, Vector4 minimum, Vector4 maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        RangeBounds bounds = RangeBounds.Closed,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null)
    {
        ValidateRangeBounds(bounds);
        Number(minimum, minimumParamName);
        Number(maximum, maximumParamName);
        if (!(bounds == RangeBounds.Closed
            ? NumericValidation.IsLessThanOrEqual(minimum, maximum)
            : NumericValidation.IsLessThan(minimum, maximum)))
        {
            ThrowOutOfRange(maximum, "Bounds must be ordered and describe a nonempty range on every axis.", maximumParamName);
        }

        return NumericValidation.IsInRange(value, minimum, maximum, bounds)
            ? value : throw CreateRangeException(value, minimum, maximum, bounds, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f Number(Vector4f value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNumber(value) ? value : throw CreateOutOfRange(value, "Value must not be NaN.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f Finite(Vector4f value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsFinite(value) ? value : throw CreateOutOfRange(value, "Value must be finite.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4f NonZero(Vector4f value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNonZero(value) ? value : throw CreateOutOfRange(value, "Value must be finite and nonzero.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d Number(Vector2d value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNumber(value) ? value : throw CreateOutOfRange(value, "Value must not be NaN.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d Finite(Vector2d value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsFinite(value) ? value : throw CreateOutOfRange(value, "Value must be finite.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2d NonZero(Vector2d value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNonZero(value) ? value : throw CreateOutOfRange(value, "Value must be finite and nonzero.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d Number(Vector3d value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNumber(value) ? value : throw CreateOutOfRange(value, "Value must not be NaN.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d Finite(Vector3d value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsFinite(value) ? value : throw CreateOutOfRange(value, "Value must be finite.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3d NonZero(Vector3d value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNonZero(value) ? value : throw CreateOutOfRange(value, "Value must be finite and nonzero.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d Number(Vector4d value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNumber(value) ? value : throw CreateOutOfRange(value, "Value must not be NaN.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d Finite(Vector4d value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsFinite(value) ? value : throw CreateOutOfRange(value, "Value must be finite.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4d NonZero(Vector4d value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNonZero(value) ? value : throw CreateOutOfRange(value, "Value must be finite and nonzero.", paramName);

}
