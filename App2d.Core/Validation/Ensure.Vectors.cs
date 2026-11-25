using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Validation;

public static partial class Ensure
{
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

public static partial class NumericValidation
{
    public static bool IsNumber(Vector2f value) => HasNoNaN<Vector2f, float>(value);
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsFinite(Vector2f value) => value.IsFinite();
    public static bool IsPositive(Vector2f value) => IsPositive(value.X) && IsPositive(value.Y);
    public static bool IsNegative(Vector2f value, bool allowNegativeInfinity = false) => IsNegative(value.X, allowNegativeInfinity) && IsNegative(value.Y, allowNegativeInfinity);
    public static bool IsNonNegative(Vector2f value, bool allowInfinity = false) => IsNonNegative(value.X, allowInfinity) && IsNonNegative(value.Y, allowInfinity);
    public static bool IsNonPositive(Vector2f value) => IsNonPositive(value.X) && IsNonPositive(value.Y);
    public static bool IsNonZero(Vector2f value) => IsNonZeroVector<Vector2f, float>(value);

    public static bool IsLessThan(Vector2f value, Vector2f limit) => value.X < limit.X && value.Y < limit.Y;
    public static bool IsLessThanOrEqual(Vector2f value, Vector2f limit) => value.X <= limit.X && value.Y <= limit.Y;
    public static bool IsGreaterThan(Vector2f value, Vector2f limit) => IsLessThan(limit, value);
    public static bool IsGreaterThanOrEqual(Vector2f value, Vector2f limit) => IsLessThanOrEqual(limit, value);
    public static bool IsInRange(Vector2f value, Vector2f minimum, Vector2f maximum, RangeBounds bounds = RangeBounds.Closed) =>
        IsInRange(value.X, minimum.X, maximum.X, bounds) &&
        IsInRange(value.Y, minimum.Y, maximum.Y, bounds);

    public static bool IsNumber(Vector3f value) => HasNoNaN<Vector3f, float>(value);
    public static bool IsFinite(Vector3f value) => value.IsFinite();
    public static bool IsPositive(Vector3f value) => IsPositive(value.X) && IsPositive(value.Y) && IsPositive(value.Z);
    public static bool IsNegative(Vector3f value, bool allowNegativeInfinity = false) => IsNegative(value.X, allowNegativeInfinity) && IsNegative(value.Y, allowNegativeInfinity) && IsNegative(value.Z, allowNegativeInfinity);
    public static bool IsNonNegative(Vector3f value, bool allowInfinity = false) => IsNonNegative(value.X, allowInfinity) && IsNonNegative(value.Y, allowInfinity) && IsNonNegative(value.Z, allowInfinity);
    public static bool IsNonPositive(Vector3f value) => IsNonPositive(value.X) && IsNonPositive(value.Y) && IsNonPositive(value.Z);
    public static bool IsNonZero(Vector3f value) => IsNonZeroVector<Vector3f, float>(value);
    public static bool IsLessThan(Vector3f value, Vector3f limit) => value.X < limit.X && value.Y < limit.Y && value.Z < limit.Z;
    public static bool IsLessThanOrEqual(Vector3f value, Vector3f limit) => value.X <= limit.X && value.Y <= limit.Y && value.Z <= limit.Z;
    public static bool IsGreaterThan(Vector3f value, Vector3f limit) => IsLessThan(limit, value);
    public static bool IsGreaterThanOrEqual(Vector3f value, Vector3f limit) => IsLessThanOrEqual(limit, value);
    public static bool IsInRange(Vector3f value, Vector3f minimum, Vector3f maximum, RangeBounds bounds = RangeBounds.Closed) =>
        IsInRange(value.X, minimum.X, maximum.X, bounds) &&
        IsInRange(value.Y, minimum.Y, maximum.Y, bounds) &&
        IsInRange(value.Z, minimum.Z, maximum.Z, bounds);

    public static bool IsNumber(Vector4 value) => IsNumber(value.ToVector4f());
    public static bool IsFinite(Vector4 value) => IsFinite(value.ToVector4f());
    public static bool IsPositive(Vector4 value) => IsPositive(value.X) && IsPositive(value.Y) && IsPositive(value.Z) && IsPositive(value.W);
    public static bool IsNegative(Vector4 value, bool allowNegativeInfinity = false) => IsNegative(value.X, allowNegativeInfinity) && IsNegative(value.Y, allowNegativeInfinity) && IsNegative(value.Z, allowNegativeInfinity) && IsNegative(value.W, allowNegativeInfinity);
    public static bool IsNonNegative(Vector4 value, bool allowInfinity = false) => IsNonNegative(value.X, allowInfinity) && IsNonNegative(value.Y, allowInfinity) && IsNonNegative(value.Z, allowInfinity) && IsNonNegative(value.W, allowInfinity);
    public static bool IsNonPositive(Vector4 value) => IsNonPositive(value.X) && IsNonPositive(value.Y) && IsNonPositive(value.Z) && IsNonPositive(value.W);
    public static bool IsNonZero(Vector4 value) => IsNonZero(value.ToVector4f());
    public static bool IsLessThan(Vector4 value, Vector4 limit) => value.X < limit.X && value.Y < limit.Y && value.Z < limit.Z && value.W < limit.W;
    public static bool IsLessThanOrEqual(Vector4 value, Vector4 limit) => value.X <= limit.X && value.Y <= limit.Y && value.Z <= limit.Z && value.W <= limit.W;
    public static bool IsGreaterThan(Vector4 value, Vector4 limit) => IsLessThan(limit, value);
    public static bool IsGreaterThanOrEqual(Vector4 value, Vector4 limit) => IsLessThanOrEqual(limit, value);
    public static bool IsInRange(Vector4 value, Vector4 minimum, Vector4 maximum, RangeBounds bounds = RangeBounds.Closed) =>
        IsInRange(value.X, minimum.X, maximum.X, bounds) &&
        IsInRange(value.Y, minimum.Y, maximum.Y, bounds) &&
        IsInRange(value.Z, minimum.Z, maximum.Z, bounds) &&
        IsInRange(value.W, minimum.W, maximum.W, bounds);

    public static bool IsNumber(Vector4f value) => HasNoNaN<Vector4f, float>(value);
    public static bool IsFinite(Vector4f value) => Vector4f.IsFinite(value);
    public static bool IsNonZero(Vector4f value) => IsNonZeroVector<Vector4f, float>(value);

    public static bool IsNumber(Vector2d value) => HasNoNaN<Vector2d, double>(value);
    public static bool IsFinite(Vector2d value) => Vector2d.IsFinite(value);
    public static bool IsNonZero(Vector2d value) => IsNonZeroVector<Vector2d, double>(value);

    public static bool IsNumber(Vector3d value) => HasNoNaN<Vector3d, double>(value);
    public static bool IsFinite(Vector3d value) => Vector3d.IsFinite(value);
    public static bool IsNonZero(Vector3d value) => IsNonZeroVector<Vector3d, double>(value);

    public static bool IsNumber(Vector4d value) => HasNoNaN<Vector4d, double>(value);
    public static bool IsFinite(Vector4d value) => Vector4d.IsFinite(value);
    public static bool IsNonZero(Vector4d value) => IsNonZeroVector<Vector4d, double>(value);

    private static bool HasNoNaN<TVector, TScalar>(TVector value)
        where TVector : struct, INumberTuple<TVector, TScalar>
        where TScalar : INumber<TScalar>
    {
        for (var axis = 0; axis < TVector.Dimension; axis++)
        {
            if (!IsNumber(value[axis])) return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsNonZeroVector<TVector, TScalar>(TVector value)
        where TVector : unmanaged, IVector<TVector, TScalar>, IEquatable<TVector>
        where TScalar : unmanaged, IFloatingPointIeee754<TScalar> =>
        TVector.IsFinite(value) && !value.Equals(TVector.Zero);
}
