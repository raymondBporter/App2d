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
        if (allowInfinity) Number(value, paramName); else Finite(value, paramName);
        if (allowInfinity) Number(maximum, maximumParamName); else Finite(maximum, maximumParamName);
        return NumericValidation.IsLessThan(value, maximum) ? value
            : throw CreateOutOfRange(value, $"Value must be < {maximum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T LessThanOrEqual<T>(T value, T maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null, bool allowInfinity = false) where T : INumber<T>
    {
        if (allowInfinity) Number(value, paramName); else Finite(value, paramName);
        if (allowInfinity) Number(maximum, maximumParamName); else Finite(maximum, maximumParamName);
        return NumericValidation.IsLessThanOrEqual(value, maximum) ? value
            : throw CreateOutOfRange(value, $"Value must be <= {maximum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GreaterThan<T>(T value, T minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null, bool allowInfinity = false) where T : INumber<T>
    {
        if (allowInfinity) Number(value, paramName); else Finite(value, paramName);
        if (allowInfinity) Number(minimum, minimumParamName); else Finite(minimum, minimumParamName);
        return NumericValidation.IsGreaterThan(value, minimum) ? value
            : throw CreateOutOfRange(value, $"Value must be > {minimum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GreaterThanOrEqual<T>(T value, T minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null, bool allowInfinity = false) where T : INumber<T>
    {
        if (allowInfinity) Number(value, paramName); else Finite(value, paramName);
        if (allowInfinity) Number(minimum, minimumParamName); else Finite(minimum, minimumParamName);
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

    private static void ValidateRangeBounds(RangeBounds bounds)
    {
        if (bounds is < RangeBounds.Closed or > RangeBounds.OpenClosed)
            ThrowOutOfRange(bounds, "Unknown range endpoints.");
    }
}
