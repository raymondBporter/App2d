using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core;

public static class ArgGuard
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull<T>(
        [NotNull] T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value is null)
            ThrowNullCore(paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 RequireFinitePositive(
        Vector2 value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ThrowIfNotPositiveFiniteValue(value.X, paramName);
        ThrowIfNotPositiveFiniteValue(value.Y, paramName);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T RequireNotNull<T>(
        [NotNull] T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where T : class
    {
        return value ?? throw CreateNull(paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string RequireNotNullOrWhitespace(
    string? value,
    [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNullOrWhiteSpace(
        [NotNull] string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value is null)
            ThrowNullCore(paramName);
        if (string.IsNullOrWhiteSpace(value))
            ThrowArgumentCore(paramName, "Value cannot be empty or whitespace.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFinite(
        float value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!float.IsFinite(value))
            ThrowCore(paramName, value, "Value must be finite.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFinite(
        Vector2 value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!float.IsFinite(value.X) || !float.IsFinite(value.Y))
            ThrowCore(paramName, value, "Both vector components must be finite.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFiniteOrZero(
        Vector2 value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!float.IsFinite(value.X) ||
            !float.IsFinite(value.Y) ||
            (value.X == 0f && value.Y == 0f))
        {
            ThrowCore(paramName, value, "Vector must be finite and non-zero.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotPositive(
        int value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value <= 0)
            ThrowCore(paramName, value, "Value must be positive.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotPositive(
        float value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!float.IsFinite(value) || value <= 0f)
            ThrowCore(paramName, value, "Value must be positive and finite.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotPositive(
        Vector2 value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!float.IsFinite(value.X) ||
            !float.IsFinite(value.Y) ||
            value.X <= 0f ||
            value.Y <= 0f)
        {
            ThrowCore(paramName, value, "Both vector components must be positive and finite.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotComponentWiseLessThan(
        Vector2 value,
        Vector2 upperExclusive,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(upperExclusive))] string? upperParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(upperExclusive, upperParamName);
        if (value.X >= upperExclusive.X || value.Y >= upperExclusive.Y)
        {
            ThrowCore(
                paramName,
                value,
                $"Each vector component must be less than the corresponding component of {upperExclusive}.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegativeOrNotFinite(
        float value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!float.IsFinite(value) || value < 0f)
            ThrowCore(paramName, value, "Value must be finite and non-negative.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotPositiveFiniteValue(
        float value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!float.IsFinite(value) || value <= 0f)
            ThrowCore(paramName, value, "Value must be greater that zero and finite.");
    }

    // Useful for unbounded ray lengths: positive infinity is valid, NaN and negatives are not.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNegativeOrNaN(
        float value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (float.IsNaN(value) || value < 0f)
            ThrowCore(paramName, value, "Value must be non-negative and not NaN.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfLessThan(
        int value,
        int minimum,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < minimum)
            ThrowCore(paramName, value, $"Value must be at least {minimum}.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfTooShort<T>(
        ReadOnlySpan<T> value,
        int minimumLength,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value.Length < minimumLength)
        {
            ThrowCore(
                paramName,
                value.Length,
                $"Collection must contain at least {minimumLength} items.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfContainsNull<T>(
        ReadOnlySpan<T> value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        foreach (var item in value)
        {
            if (item is null)
                ThrowArgumentCore(paramName, "Collection cannot contain null values.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfLessThanOrEqual(
        float value,
        float minimum,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!float.IsFinite(value) || value <= minimum)
            ThrowCore(paramName, value, $"Value must be finite and greater than {minimum}.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfGreaterThanOrEqual(
        float value,
        float upperExclusive,
        [CallerArgumentExpression(nameof(value))] string? paramName = null,
        [CallerArgumentExpression(nameof(upperExclusive))] string? upperParamName = null)
    {
        ThrowIfNotFinite(value, paramName);
        ThrowIfNotFinite(upperExclusive, upperParamName);
        if (value >= upperExclusive)
            ThrowCore(paramName, value, $"Value must be finite and less than {upperExclusive}.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotInClosedRange(
        float value,
        float minimum,
        float maximum,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!float.IsFinite(value) || value < minimum || value > maximum)
        {
            ThrowCore(
                paramName,
                value,
                $"Value must be finite and between {minimum} and {maximum}, inclusive.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfSameReference<T>(
        T first,
        T second,
        string message,
        [CallerArgumentExpression(nameof(first))] string? paramName = null)
        where T : class
    {
        if (ReferenceEquals(first, second))
            ThrowArgumentCore(paramName, message);
    }

    public static TExpected RequireType<TExpected>(
        object? value,
        string message,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        value is TExpected typedValue
            ? typedValue
            : throw CreateInvalid(message, paramName);

    [DoesNotReturn]
    public static void ThrowInvalid(string message, string? paramName = null) => ThrowArgumentCore(paramName, message);

    [DoesNotReturn]
    public static void ThrowInvalid<T>(T _, string message, [CallerArgumentExpression(nameof(_))] string? paramName = null) =>
        ThrowArgumentCore(paramName, message);

    public static ArgumentException CreateInvalid(string message, string? paramName = null) => new(message, paramName);

    public static ArgumentNullException CreateNull(string? paramName = null) => new(paramName);

    [DoesNotReturn]
    public static void ThrowOutOfRange<T>(
        T actualValue,
        string message,
        [CallerArgumentExpression(nameof(actualValue))] string? paramName = null) =>
        throw CreateOutOfRange(actualValue, message, paramName);

    public static ArgumentOutOfRangeException CreateOutOfRange<T>(
        T actualValue,
        string message,
        [CallerArgumentExpression(nameof(actualValue))] string? paramName = null) =>
        new(paramName, actualValue, message);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCore(string? paramName, object? actualValue, string message) =>
        throw new ArgumentOutOfRangeException(paramName, actualValue, message);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowNullCore(string? paramName) =>
        throw new ArgumentNullException(paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowArgumentCore(string? paramName, string message) =>
        throw new ArgumentException(message, paramName);

}
