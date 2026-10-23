using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Validation;

public static partial class ArgGuard
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Number<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T> =>
        !T.IsNaN(value) ? value : throw CreateOutOfRange(value, "Value must not be NaN.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Finite<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T> =>
        NumericValidation.IsFinite(value) ? value : throw CreateOutOfRange(value, "Value must be finite.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T Positive<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T> =>
        NumericValidation.IsPositive(value) ? value : throw CreateOutOfRange(value, "Value must be finite and positive (> 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NonZero<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : INumber<T> =>
        NumericValidation.IsNonZero(value) ? value : throw CreateOutOfRange(value, "Value must be finite and nonzero.", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T NonNegative<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null, bool allowInfinity = false) where T : INumber<T> =>
        (allowInfinity ? value >= T.Zero : NumericValidation.IsNonNegative(value))
            ? value : throw CreateOutOfRange(value, allowInfinity
                ? "Value must be nonnegative (>= 0) and not NaN."
                : "Value must be finite and nonnegative (>= 0).", paramName);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T AtLeast<T>(T value, T minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null) where T : INumber<T>
    {
        Finite(value, paramName);
        Finite(minimum, minimumParamName);
        return value >= minimum ? value : throw CreateOutOfRange(value, $"Value must be at least {minimum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T GreaterThan<T>(T value, T minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(minimum))] string? minimumParamName = null) where T : INumber<T>
    {
        Finite(value, paramName);
        Finite(minimum, minimumParamName);
        return value > minimum ? value : throw CreateOutOfRange(value, $"Value must be greater than {minimum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T AtMost<T>(T value, T maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null) where T : INumber<T>
    {
        Finite(value, paramName);
        Finite(maximum, maximumParamName);
        return value <= maximum ? value : throw CreateOutOfRange(value, $"Value must be at most {maximum}.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T LessThan<T>(T value, T maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(maximum))] string? maximumParamName = null) where T : INumber<T>
    {
        Finite(value, paramName);
        Finite(maximum, maximumParamName);
        return value < maximum ? value : throw CreateOutOfRange(value, $"Value must be less than {maximum}.", paramName);
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
            ? value : throw CreateRangeException(value, minimum, maximum, bounds, paramName);
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
