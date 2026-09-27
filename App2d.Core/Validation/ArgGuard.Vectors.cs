using App2d.Core.Validation;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core;

public static partial class ArgGuard
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFinite(Vector2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsFinite(value)) ThrowNotFiniteException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfZero(Vector2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (NumericValidation.IsZero(value)) ThrowZeroException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotPositive(Vector2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsPositive(value)) ThrowNotPositiveException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegative(Vector2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (NumericValidation.HasNegativeComponent(value)) ThrowNegativeException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrZero(Vector2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsFiniteAndNonZero(value)) ThrowNotFiniteOrZeroException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotPositive(Vector2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsFiniteAndPositive(value)) ThrowNotFiniteOrNotPositiveException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNegative(Vector2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsFiniteAndNonNegative(value)) ThrowNotFiniteOrNegativeException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotComponentWiseLessThan(Vector2 value, Vector2 limit, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsComponentWiseLessThan(value, limit))
            ThrowNotComponentWiseLessThanException(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotComponentWiseLessThan(Vector2 value, Vector2 limit,
        [CallerArgumentExpression(nameof(value))] string? paramName = null, [CallerArgumentExpression(nameof(limit))] string? limitParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(limit, limitParamName);
        ThrowIfNotComponentWiseLessThan(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotComponentWiseLessThanOrEqual(Vector2 value, Vector2 limit, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsComponentWiseLessThanOrEqual(value, limit))
            ThrowNotComponentWiseLessThanOrEqualException(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotComponentWiseLessThanOrEqual(Vector2 value, Vector2 limit,
        [CallerArgumentExpression(nameof(value))] string? paramName = null, [CallerArgumentExpression(nameof(limit))] string? limitParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(limit, limitParamName);
        ThrowIfNotComponentWiseLessThanOrEqual(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotComponentWiseGreaterThan(Vector2 value, Vector2 limit, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsComponentWiseGreaterThan(value, limit))
            ThrowNotComponentWiseGreaterThanException(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotComponentWiseGreaterThan(Vector2 value, Vector2 limit,
        [CallerArgumentExpression(nameof(value))] string? paramName = null, [CallerArgumentExpression(nameof(limit))] string? limitParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(limit, limitParamName);
        ThrowIfNotComponentWiseGreaterThan(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotComponentWiseGreaterThanOrEqual(Vector2 value, Vector2 limit, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsComponentWiseGreaterThanOrEqual(value, limit))
            ThrowNotComponentWiseGreaterThanOrEqualException(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotComponentWiseGreaterThanOrEqual(Vector2 value, Vector2 limit,
        [CallerArgumentExpression(nameof(value))] string? paramName = null, [CallerArgumentExpression(nameof(limit))] string? limitParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(limit, limitParamName);
        ThrowIfNotComponentWiseGreaterThanOrEqual(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotInClosedRange(Vector2 value, Vector2 minimum, Vector2 maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsInClosedRange(value, minimum, maximum))
            ThrowNotInClosedRangeException(value, minimum, maximum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotInClosedRange(Vector2 value, Vector2 minimum, Vector2 maximum,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(minimum, minimumParamName);
        ThrowIfNotFinite(maximum, maximumParamName);
        ThrowIfNotComponentWiseGreaterThanOrEqual(maximum, minimum, maximumParamName);
        ThrowIfNotInClosedRange(value, minimum, maximum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotInOpenRange(Vector2 value, Vector2 minimum, Vector2 maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsInOpenRange(value, minimum, maximum))
            ThrowNotInOpenRangeException(value, minimum, maximum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotInOpenRange(Vector2 value, Vector2 minimum, Vector2 maximum,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(minimum, minimumParamName);
        ThrowIfNotFinite(maximum, maximumParamName);
        ThrowIfNotComponentWiseGreaterThan(maximum, minimum, maximumParamName);
        ThrowIfNotInOpenRange(value, minimum, maximum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 RequireFinitePositive(Vector2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ThrowIfNotFiniteOrNotPositive(value, paramName);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFinite(Vector3 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsFinite(value)) ThrowNotFiniteException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfZero(Vector3 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (NumericValidation.IsZero(value)) ThrowZeroException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotPositive(Vector3 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsPositive(value)) ThrowNotPositiveException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegative(Vector3 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (NumericValidation.HasNegativeComponent(value)) ThrowNegativeException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrZero(Vector3 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsFiniteAndNonZero(value)) ThrowNotFiniteOrZeroException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotPositive(Vector3 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsFiniteAndPositive(value)) ThrowNotFiniteOrNotPositiveException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNegative(Vector3 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsFiniteAndNonNegative(value)) ThrowNotFiniteOrNegativeException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotComponentWiseLessThan(Vector3 value, Vector3 limit, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsComponentWiseLessThan(value, limit))
            ThrowNotComponentWiseLessThanException(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotComponentWiseLessThan(Vector3 value, Vector3 limit,
        [CallerArgumentExpression(nameof(value))] string? paramName = null, [CallerArgumentExpression(nameof(limit))] string? limitParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(limit, limitParamName);
        ThrowIfNotComponentWiseLessThan(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotComponentWiseLessThanOrEqual(Vector3 value, Vector3 limit, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsComponentWiseLessThanOrEqual(value, limit))
            ThrowNotComponentWiseLessThanOrEqualException(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotComponentWiseLessThanOrEqual(Vector3 value, Vector3 limit,
        [CallerArgumentExpression(nameof(value))] string? paramName = null, [CallerArgumentExpression(nameof(limit))] string? limitParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(limit, limitParamName);
        ThrowIfNotComponentWiseLessThanOrEqual(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotComponentWiseGreaterThan(Vector3 value, Vector3 limit, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsComponentWiseGreaterThan(value, limit))
            ThrowNotComponentWiseGreaterThanException(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotComponentWiseGreaterThan(Vector3 value, Vector3 limit,
        [CallerArgumentExpression(nameof(value))] string? paramName = null, [CallerArgumentExpression(nameof(limit))] string? limitParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(limit, limitParamName);
        ThrowIfNotComponentWiseGreaterThan(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotComponentWiseGreaterThanOrEqual(Vector3 value, Vector3 limit, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsComponentWiseGreaterThanOrEqual(value, limit))
            ThrowNotComponentWiseGreaterThanOrEqualException(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotComponentWiseGreaterThanOrEqual(Vector3 value, Vector3 limit,
        [CallerArgumentExpression(nameof(value))] string? paramName = null, [CallerArgumentExpression(nameof(limit))] string? limitParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(limit, limitParamName);
        ThrowIfNotComponentWiseGreaterThanOrEqual(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotInClosedRange(Vector3 value, Vector3 minimum, Vector3 maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsInClosedRange(value, minimum, maximum))
            ThrowNotInClosedRangeException(value, minimum, maximum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotInClosedRange(Vector3 value, Vector3 minimum, Vector3 maximum,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(minimum, minimumParamName);
        ThrowIfNotFinite(maximum, maximumParamName);
        ThrowIfNotComponentWiseGreaterThanOrEqual(maximum, minimum, maximumParamName);
        ThrowIfNotInClosedRange(value, minimum, maximum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotInOpenRange(Vector3 value, Vector3 minimum, Vector3 maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsInOpenRange(value, minimum, maximum))
            ThrowNotInOpenRangeException(value, minimum, maximum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotInOpenRange(Vector3 value, Vector3 minimum, Vector3 maximum,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(minimum, minimumParamName);
        ThrowIfNotFinite(maximum, maximumParamName);
        ThrowIfNotComponentWiseGreaterThan(maximum, minimum, maximumParamName);
        ThrowIfNotInOpenRange(value, minimum, maximum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 RequireFinitePositive(Vector3 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ThrowIfNotFiniteOrNotPositive(value, paramName);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFinite(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsFinite(value)) ThrowNotFiniteException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfZero(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (NumericValidation.IsZero(value)) ThrowZeroException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotPositive(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsPositive(value)) ThrowNotPositiveException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegative(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (NumericValidation.HasNegativeComponent(value)) ThrowNegativeException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrZero(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsFiniteAndNonZero(value)) ThrowNotFiniteOrZeroException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotPositive(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsFiniteAndPositive(value)) ThrowNotFiniteOrNotPositiveException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNegative(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsFiniteAndNonNegative(value)) ThrowNotFiniteOrNegativeException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotComponentWiseLessThan(Vector4 value, Vector4 limit, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsComponentWiseLessThan(value, limit))
            ThrowNotComponentWiseLessThanException(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotComponentWiseLessThan(Vector4 value, Vector4 limit,
        [CallerArgumentExpression(nameof(value))] string? paramName = null, [CallerArgumentExpression(nameof(limit))] string? limitParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(limit, limitParamName);
        ThrowIfNotComponentWiseLessThan(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotComponentWiseLessThanOrEqual(Vector4 value, Vector4 limit, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsComponentWiseLessThanOrEqual(value, limit))
            ThrowNotComponentWiseLessThanOrEqualException(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotComponentWiseLessThanOrEqual(Vector4 value, Vector4 limit,
        [CallerArgumentExpression(nameof(value))] string? paramName = null, [CallerArgumentExpression(nameof(limit))] string? limitParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(limit, limitParamName);
        ThrowIfNotComponentWiseLessThanOrEqual(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotComponentWiseGreaterThan(Vector4 value, Vector4 limit, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsComponentWiseGreaterThan(value, limit))
            ThrowNotComponentWiseGreaterThanException(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotComponentWiseGreaterThan(Vector4 value, Vector4 limit,
        [CallerArgumentExpression(nameof(value))] string? paramName = null, [CallerArgumentExpression(nameof(limit))] string? limitParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(limit, limitParamName);
        ThrowIfNotComponentWiseGreaterThan(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotComponentWiseGreaterThanOrEqual(Vector4 value, Vector4 limit, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsComponentWiseGreaterThanOrEqual(value, limit))
            ThrowNotComponentWiseGreaterThanOrEqualException(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotComponentWiseGreaterThanOrEqual(Vector4 value, Vector4 limit,
        [CallerArgumentExpression(nameof(value))] string? paramName = null, [CallerArgumentExpression(nameof(limit))] string? limitParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(limit, limitParamName);
        ThrowIfNotComponentWiseGreaterThanOrEqual(value, limit, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotInClosedRange(Vector4 value, Vector4 minimum, Vector4 maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsInClosedRange(value, minimum, maximum))
            ThrowNotInClosedRangeException(value, minimum, maximum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotInClosedRange(Vector4 value, Vector4 minimum, Vector4 maximum,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(minimum, minimumParamName);
        ThrowIfNotFinite(maximum, maximumParamName);
        ThrowIfNotComponentWiseGreaterThanOrEqual(maximum, minimum, maximumParamName);
        ThrowIfNotInClosedRange(value, minimum, maximum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotInOpenRange(Vector4 value, Vector4 minimum, Vector4 maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsInOpenRange(value, minimum, maximum))
            ThrowNotInOpenRangeException(value, minimum, maximum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotInOpenRange(Vector4 value, Vector4 minimum, Vector4 maximum,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(minimum, minimumParamName);
        ThrowIfNotFinite(maximum, maximumParamName);
        ThrowIfNotComponentWiseGreaterThan(maximum, minimum, maximumParamName);
        ThrowIfNotInOpenRange(value, minimum, maximum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 RequireFinitePositive(Vector4 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ThrowIfNotFiniteOrNotPositive(value, paramName);
        return value;
    }

}
