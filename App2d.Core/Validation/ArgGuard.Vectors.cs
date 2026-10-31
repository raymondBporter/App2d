using App2d.Core.Mathematics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Validation;

public static partial class ArgGuard
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Number(Vector2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        !float.IsNaN(value.X) && !float.IsNaN(value.Y) ? value : throw CreateOutOfRange(value, "No component may be NaN.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Finite(Vector2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsFinite(value) ? value : throw CreateOutOfRange(value, "Value must be finite.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Positive(Vector2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsPositive(value) ? value : throw CreateOutOfRange(value, "Value must be finite and positive (> 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 NonZero(Vector2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNonZero(value) ? value : throw CreateOutOfRange(value, "Value must be finite and nonzero.", paramName);

    public static Vector2d NonZero(Vector2d value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNonZero(value) ? value : throw CreateOutOfRange(value, "Value must be finite and nonzero.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 NonNegative(Vector2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null, bool allowInfinity = false) =>
        (allowInfinity ? value.X >= 0f && value.Y >= 0f : NumericValidation.IsNonNegative(value)) ? value
            : throw CreateOutOfRange(value, allowInfinity
                ? "Every component must be nonnegative (>= 0) and not NaN."
                : "Every component must be finite and nonnegative (>= 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 AtLeast(Vector2 value, Vector2 minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null)
    {
        Finite(value, paramName);
        Finite(minimum, minimumParamName);
        return NumericValidation.IsComponentWiseGreaterThanOrEqual(value, minimum) ? value
            : throw CreateOutOfRange(value, $"Every component must be at least the corresponding component of {minimum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 GreaterThan(Vector2 value, Vector2 minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null)
    {
        Finite(value, paramName);
        Finite(minimum, minimumParamName);
        return NumericValidation.IsComponentWiseGreaterThan(value, minimum) ? value
            : throw CreateOutOfRange(value, $"Every component must be greater than the corresponding component of {minimum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 AtMost(Vector2 value, Vector2 maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null)
    {
        Finite(value, paramName);
        Finite(maximum, maximumParamName);
        return NumericValidation.IsComponentWiseLessThanOrEqual(value, maximum) ? value
            : throw CreateOutOfRange(value, $"Every component must be at most the corresponding component of {maximum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 LessThan(Vector2 value, Vector2 maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null)
    {
        Finite(value, paramName);
        Finite(maximum, maximumParamName);
        return NumericValidation.IsComponentWiseLessThan(value, maximum) ? value
            : throw CreateOutOfRange(value, $"Every component must be less than the corresponding component of {maximum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 InRange(Vector2 value, Vector2 minimum, Vector2 maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        RangeBounds bounds = RangeBounds.Closed,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null)
    {
        ValidateRangeBounds(bounds);
        Number(minimum, minimumParamName);
        Number(maximum, maximumParamName);
        if (!(bounds == RangeBounds.Closed
            ? NumericValidation.IsComponentWiseLessThanOrEqual(minimum, maximum)
            : NumericValidation.IsComponentWiseLessThan(minimum, maximum)))
        {
            ThrowOutOfRange(maximum, "Bounds must be ordered and describe a nonempty range on every axis.", maximumParamName);
        }

        return NumericValidation.IsInRange(value, minimum, maximum, bounds)
            ? value : throw CreateRangeException(value, minimum, maximum, bounds, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Number(Vector3 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        !float.IsNaN(value.X) && !float.IsNaN(value.Y) && !float.IsNaN(value.Z) ? value : throw CreateOutOfRange(value, "No component may be NaN.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Finite(Vector3 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsFinite(value) ? value : throw CreateOutOfRange(value, "Value must be finite.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 Positive(Vector3 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsPositive(value) ? value : throw CreateOutOfRange(value, "Value must be finite and positive (> 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 NonZero(Vector3 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNonZero(value) ? value : throw CreateOutOfRange(value, "Value must be finite and nonzero.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 NonNegative(Vector3 value, [CallerArgumentExpression(nameof(value))] string? paramName = null, bool allowInfinity = false) =>
        (allowInfinity ? value.X >= 0f && value.Y >= 0f && value.Z >= 0f : NumericValidation.IsNonNegative(value)) ? value
            : throw CreateOutOfRange(value, allowInfinity
                ? "Every component must be nonnegative (>= 0) and not NaN."
                : "Every component must be finite and nonnegative (>= 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 AtLeast(Vector3 value, Vector3 minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null)
    {
        Finite(value, paramName);
        Finite(minimum, minimumParamName);
        return NumericValidation.IsComponentWiseGreaterThanOrEqual(value, minimum) ? value
            : throw CreateOutOfRange(value, $"Every component must be at least the corresponding component of {minimum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 GreaterThan(Vector3 value, Vector3 minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null)
    {
        Finite(value, paramName);
        Finite(minimum, minimumParamName);
        return NumericValidation.IsComponentWiseGreaterThan(value, minimum) ? value
            : throw CreateOutOfRange(value, $"Every component must be greater than the corresponding component of {minimum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 AtMost(Vector3 value, Vector3 maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null)
    {
        Finite(value, paramName);
        Finite(maximum, maximumParamName);
        return NumericValidation.IsComponentWiseLessThanOrEqual(value, maximum) ? value
            : throw CreateOutOfRange(value, $"Every component must be at most the corresponding component of {maximum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 LessThan(Vector3 value, Vector3 maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null)
    {
        Finite(value, paramName);
        Finite(maximum, maximumParamName);
        return NumericValidation.IsComponentWiseLessThan(value, maximum) ? value
            : throw CreateOutOfRange(value, $"Every component must be less than the corresponding component of {maximum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 InRange(Vector3 value, Vector3 minimum, Vector3 maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        RangeBounds bounds = RangeBounds.Closed,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null)
    {
        ValidateRangeBounds(bounds);
        Number(minimum, minimumParamName);
        Number(maximum, maximumParamName);
        if (!(bounds == RangeBounds.Closed
            ? NumericValidation.IsComponentWiseLessThanOrEqual(minimum, maximum)
            : NumericValidation.IsComponentWiseLessThan(minimum, maximum)))
        {
            ThrowOutOfRange(maximum, "Bounds must be ordered and describe a nonempty range on every axis.", maximumParamName);
        }

        return NumericValidation.IsInRange(value, minimum, maximum, bounds)
            ? value : throw CreateRangeException(value, minimum, maximum, bounds, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Number(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        !float.IsNaN(value.X) && !float.IsNaN(value.Y) && !float.IsNaN(value.Z) && !float.IsNaN(value.W) ? value : throw CreateOutOfRange(value, "No component may be NaN.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Finite(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsFinite(value) ? value : throw CreateOutOfRange(value, "Value must be finite.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 Positive(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsPositive(value) ? value : throw CreateOutOfRange(value, "Value must be finite and positive (> 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 NonZero(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsNonZero(value) ? value : throw CreateOutOfRange(value, "Value must be finite and nonzero.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 NonNegative(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null, bool allowInfinity = false) =>
        (allowInfinity ? value.X >= 0f && value.Y >= 0f && value.Z >= 0f && value.W >= 0f : NumericValidation.IsNonNegative(value)) ? value
            : throw CreateOutOfRange(value, allowInfinity
                ? "Every component must be nonnegative (>= 0) and not NaN."
                : "Every component must be finite and nonnegative (>= 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 AtLeast(Vector4 value, Vector4 minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null)
    {
        Finite(value, paramName);
        Finite(minimum, minimumParamName);
        return NumericValidation.IsComponentWiseGreaterThanOrEqual(value, minimum) ? value
            : throw CreateOutOfRange(value, $"Every component must be at least the corresponding component of {minimum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 GreaterThan(Vector4 value, Vector4 minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null)
    {
        Finite(value, paramName);
        Finite(minimum, minimumParamName);
        return NumericValidation.IsComponentWiseGreaterThan(value, minimum) ? value
            : throw CreateOutOfRange(value, $"Every component must be greater than the corresponding component of {minimum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 AtMost(Vector4 value, Vector4 maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null)
    {
        Finite(value, paramName);
        Finite(maximum, maximumParamName);
        return NumericValidation.IsComponentWiseLessThanOrEqual(value, maximum) ? value
            : throw CreateOutOfRange(value, $"Every component must be at most the corresponding component of {maximum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 LessThan(Vector4 value, Vector4 maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null)
    {
        Finite(value, paramName);
        Finite(maximum, maximumParamName);
        return NumericValidation.IsComponentWiseLessThan(value, maximum) ? value
            : throw CreateOutOfRange(value, $"Every component must be less than the corresponding component of {maximum}.", paramName);
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
            ? NumericValidation.IsComponentWiseLessThanOrEqual(minimum, maximum)
            : NumericValidation.IsComponentWiseLessThan(minimum, maximum)))
        {
            ThrowOutOfRange(maximum, "Bounds must be ordered and describe a nonempty range on every axis.", maximumParamName);
        }

        return NumericValidation.IsInRange(value, minimum, maximum, bounds)
            ? value : throw CreateRangeException(value, minimum, maximum, bounds, paramName);
    }
}
