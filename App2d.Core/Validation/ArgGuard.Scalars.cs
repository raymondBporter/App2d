using App2d.Core.Validation;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core;

public static partial class ArgGuard
{
    public static T RequireFinitePositive<T>(T value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T>
    {
        ThrowIfNotFiniteOrNotPositive(value, paramName);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFinite<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T>
    {
        if (!NumericValidation.IsFinite(value)) ThrowNotFiniteException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfZero<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T>
    {
        if (NumericValidation.IsZero(value)) ThrowZeroException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotPositive<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T>
    {
        if (!NumericValidation.IsPositive(value)) ThrowNotPositiveException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegative<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T>
    {
        if (NumericValidation.IsNegative(value)) ThrowNegativeException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrZero<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T>
    {
        if (!NumericValidation.IsFiniteAndNonZero(value)) ThrowNotFiniteOrZeroException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotPositive<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T>
    {
        if (!NumericValidation.IsFiniteAndPositive(value)) ThrowNotFiniteOrNotPositiveException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNegative<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T>
    {
        if (!NumericValidation.IsFiniteAndNonNegative(value)) ThrowNotFiniteOrNegativeException(value, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegativeOrNaN<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T>
    {
        if (T.IsNaN(value) || NumericValidation.IsNegative(value))
            ThrowOutOfRange(value, "Value must be non-negative and not NaN.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfLessThan<T>(T value, T minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T>
    {
        if (NumericValidation.IsLessThan(value, minimum)) ThrowLessThanException(value, minimum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrLessThan<T>(T value, T minimum,
        [CallerArgumentExpression(nameof(value))] string? paramName = null, [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null) where T : INumber<T>
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(minimum, minimumParamName);
        ThrowIfLessThan(value, minimum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfLessThanOrEqual<T>(T value, T minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T>
    {
        if (NumericValidation.IsLessThanOrEqual(value, minimum)) ThrowLessThanOrEqualException(value, minimum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrLessThanOrEqual<T>(T value, T minimum,
        [CallerArgumentExpression(nameof(value))] string? paramName = null, [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null) where T : INumber<T>
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(minimum, minimumParamName);
        ThrowIfLessThanOrEqual(value, minimum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfGreaterThan<T>(T value, T maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T>
    {
        if (NumericValidation.IsGreaterThan(value, maximum)) ThrowGreaterThanException(value, maximum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrGreaterThan<T>(T value, T maximum,
        [CallerArgumentExpression(nameof(value))] string? paramName = null, [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null) where T : INumber<T>
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(maximum, maximumParamName);
        ThrowIfGreaterThan(value, maximum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfGreaterThanOrEqual<T>(T value, T upperExclusive, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T>
    {
        if (NumericValidation.IsGreaterThanOrEqual(value, upperExclusive)) ThrowGreaterThanOrEqualException(value, upperExclusive, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrGreaterThanOrEqual<T>(T value, T upperExclusive,
        [CallerArgumentExpression(nameof(value))] string? paramName = null, [CallerArgumentExpression(nameof(upperExclusive))] string? upperExclusiveParamName = null) where T : INumber<T>
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(upperExclusive, upperExclusiveParamName);
        ThrowIfGreaterThanOrEqual(value, upperExclusive, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotInClosedRange<T>(T value, T minimum, T maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T>
    {
        if (!NumericValidation.IsInClosedRange(value, minimum, maximum))
            ThrowNotInClosedRangeException(value, minimum, maximum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotInClosedRange<T>(T value, T minimum, T maximum,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null) where T : INumber<T>
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(minimum, minimumParamName);
        ThrowIfNotFinite(maximum, maximumParamName);
        ThrowIfLessThan(maximum, minimum, maximumParamName);
        ThrowIfNotInClosedRange(value, minimum, maximum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotInOpenRange<T>(T value, T minimum, T maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T>
    {
        if (!NumericValidation.IsInOpenRange(value, minimum, maximum))
            ThrowNotInOpenRangeException(value, minimum, maximum, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrNotInOpenRange<T>(T value, T minimum, T maximum,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null) where T : INumber<T>
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(minimum, minimumParamName);
        ThrowIfNotFinite(maximum, maximumParamName);
        ThrowIfLessThanOrEqual(maximum, minimum, maximumParamName);
        ThrowIfNotInOpenRange(value, minimum, maximum, paramName);
    }

}
