using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Validation;

public static partial class Ensure
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Number<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumberBase<T> =>
        NumericValidation.IsNumber(value) ? value : throw CreateOutOfRange(value, "Value must not be NaN.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Finite<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumberBase<T> =>
        NumericValidation.IsFinite(value) ? value : throw CreateOutOfRange(value, "Value must be finite.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NotPositiveInfinity<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumberBase<T> =>
        !T.IsPositiveInfinity(value) ? value : throw CreateOutOfRange(value, "Value must not be positive infinity.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NotNegativeInfinity<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumberBase<T> =>
        !T.IsNegativeInfinity(value) ? value : throw CreateOutOfRange(value, "Value must not be negative infinity.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Positive<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T> =>
        NumericValidation.IsPositive(value) ? value : throw CreateOutOfRange(value, "Value must be finite and positive (> 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Negative<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null, bool allowNegativeInfinity = false) where T : INumber<T> =>
        NumericValidation.IsNegative(value, allowNegativeInfinity) ? value : throw CreateOutOfRange(value, "Value must be negative (< 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NonNegative<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null, bool allowInfinity = false) where T : INumber<T> =>
        NumericValidation.IsNonNegative(value, allowInfinity) ? value : throw CreateOutOfRange(value, "Value must be nonnegative (>= 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NonPositive<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T> =>
        NumericValidation.IsNonPositive(value) ? value : throw CreateOutOfRange(value, "Value must be finite and nonpositive (<= 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NonZero<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumberBase<T> =>
        NumericValidation.IsNonZero(value) ? value : throw CreateOutOfRange(value, "Value must be finite and nonzero.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Equal<T>(T value, T expected, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T> =>
        NumericValidation.IsFinite(value) && NumericValidation.IsFinite(expected) && NumericValidation.IsEqual(value, expected)
            ? value : throw CreateOutOfRange(value, $"Value must equal {expected}.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NotEqual<T>(T value, T other, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T> =>
        NumericValidation.IsFinite(value) && NumericValidation.IsFinite(other) && NumericValidation.IsNotEqual(value, other)
            ? value : throw CreateOutOfRange(value, $"Value must not equal {other}.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T LessThan<T>(T value, T maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null, bool allowInfinity = false) where T : INumber<T>
    {
        ValidateComparisonOperands(value, maximum, allowInfinity, paramName, maximumParamName);
        return NumericValidation.IsLessThan(value, maximum) ? value
            : throw CreateOutOfRange(value, $"Value must be < {maximum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T LessThanOrEqual<T>(T value, T maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null, bool allowInfinity = false) where T : INumber<T>
    {
        ValidateComparisonOperands(value, maximum, allowInfinity, paramName, maximumParamName);
        return NumericValidation.IsLessThanOrEqual(value, maximum) ? value
            : throw CreateOutOfRange(value, $"Value must be <= {maximum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GreaterThan<T>(T value, T minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null, bool allowInfinity = false) where T : INumber<T>
    {
        ValidateComparisonOperands(value, minimum, allowInfinity, paramName, minimumParamName);
        return NumericValidation.IsGreaterThan(value, minimum) ? value
            : throw CreateOutOfRange(value, $"Value must be > {minimum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GreaterThanOrEqual<T>(T value, T minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null, bool allowInfinity = false) where T : INumber<T>
    {
        ValidateComparisonOperands(value, minimum, allowInfinity, paramName, minimumParamName);
        return NumericValidation.IsGreaterThanOrEqual(value, minimum) ? value
            : throw CreateOutOfRange(value, $"Value must be >= {minimum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T InRange<T>(T value, T minimum, T maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        RangeBounds bounds = RangeBounds.Closed,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null) where T : INumber<T>
    {
        ValidateRangeBounds(bounds);
        Number(minimum, minimumParamName);
        Number(maximum, maximumParamName);
        if (!(bounds == RangeBounds.Closed ? minimum <= maximum : minimum < maximum))
            ThrowOutOfRange(maximum, "Bounds must be ordered and describe a nonempty range.", maximumParamName);
        return NumericValidation.IsInRange(value, minimum, maximum, bounds)
            ? value
            : throw CreateRangeException(value, minimum, maximum, bounds, paramName);
    }

    /// <summary>Requires an index in [0, count), including when the collection is empty.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Index(int value, int count, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(count))] string? countParamName = null)
    {
        NonNegative(count, countParamName);
        return (uint)value < (uint)count ? value
            : throw CreateRangeException(value, 0, count, RangeBounds.ClosedOpen, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ValidateComparisonOperands<T>(T value, T limit, bool allowInfinity, string? paramName, string? limitParamName)
        where T : INumberBase<T>
    {
        if (allowInfinity)
        {
            Number(value, paramName);
            Number(limit, limitParamName);
        }
        else
        {
            Finite(value, paramName);
            Finite(limit, limitParamName);
        }
    }

    private static void ValidateRangeBounds(RangeBounds bounds)
    {
        if (bounds is < RangeBounds.Closed or > RangeBounds.OpenClosed)
            ThrowOutOfRange(bounds, "Unknown range endpoints.");
    }
}

/// <summary>
/// Nonthrowing numeric predicates shared by <see cref="Ensure"/> and other callers.
/// Sign, nonzero, and range checks require finite values by default.
/// Ordering uses ordinary operators and allows infinity.
/// </summary>
public static partial class NumericValidation
{
    public static bool IsNumber<T>(T value) where T : INumberBase<T> => !T.IsNaN(value);
    public static bool IsFinite<T>(T value) where T : INumberBase<T> => T.IsFinite(value);
    public static bool IsPositive<T>(T value) where T : INumber<T> => IsFinite(value) && value > T.Zero;
    public static bool IsNegative<T>(T value, bool allowNegativeInfinity = false) where T : INumber<T> =>
        (allowNegativeInfinity || IsFinite(value)) && value < T.Zero;
    public static bool IsNonNegative<T>(T value, bool allowInfinity = false) where T : INumber<T> =>
        (allowInfinity || IsFinite(value)) && value >= T.Zero;
    public static bool IsNonPositive<T>(T value) where T : INumber<T> => IsFinite(value) && value <= T.Zero;
    public static bool IsNonZero<T>(T value) where T : INumberBase<T> => IsFinite(value) && value != T.Zero;
    public static bool IsEqual<T>(T value, T expected) where T : INumberBase<T> => value == expected;
    public static bool IsNotEqual<T>(T value, T other) where T : INumberBase<T> => value != other;
    public static bool IsLessThan<T>(T value, T limit) where T : INumber<T> => value < limit;
    public static bool IsLessThanOrEqual<T>(T value, T limit) where T : INumber<T> => value <= limit;
    public static bool IsGreaterThan<T>(T value, T limit) where T : INumber<T> => value > limit;
    public static bool IsGreaterThanOrEqual<T>(T value, T limit) where T : INumber<T> => value >= limit;
    public static bool IsInRange<T>(T value, T minimum, T maximum, RangeBounds bounds = RangeBounds.Closed) where T : INumber<T> =>
        IsFinite(value) && (bounds switch
        {
            RangeBounds.Closed => value >= minimum && value <= maximum,
            RangeBounds.Open => value > minimum && value < maximum,
            RangeBounds.ClosedOpen => value >= minimum && value < maximum,
            RangeBounds.OpenClosed => value > minimum && value <= maximum,
            _ => false
        });
}
