using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace App2d.Core;

public static partial class ArgGuard
{
    // These cold helpers allocate a fresh exception only on failure. Never cache exception instances.
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNotFiniteException<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        throw CreateOutOfRange(value, "Value must be finite.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowZeroException<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        throw CreateOutOfRange(value, "Value must be non-zero.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNotPositiveException<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        throw CreateOutOfRange(value, "Value must be positive.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNegativeException<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        throw CreateOutOfRange(value, "Value must be non-negative.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNotFiniteOrZeroException<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        throw CreateOutOfRange(value, "Value must be finite and non-zero.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNotFiniteOrNotPositiveException<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        throw CreateOutOfRange(value, "Value must be finite and positive.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNotFiniteOrNegativeException<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        throw CreateOutOfRange(value, "Value must be finite and non-negative.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowLessThanException<T>(T value, T minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        throw CreateOutOfRange(value, $"Value must be at least {minimum}.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowLessThanOrEqualException<T>(T value, T minimum, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        throw CreateOutOfRange(value, $"Value must be greater than {minimum}.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowGreaterThanException<T>(T value, T maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        throw CreateOutOfRange(value, $"Value must be at most {maximum}.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowGreaterThanOrEqualException<T>(T value, T upperExclusive, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        throw CreateOutOfRange(value, $"Value must be less than {upperExclusive}.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNotComponentWiseLessThanException<T>(T value, T limit, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        throw CreateOutOfRange(value, $"Each component must be less than the corresponding component of {limit}.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNotComponentWiseLessThanOrEqualException<T>(T value, T limit, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        throw CreateOutOfRange(value, $"Each component must be at most the corresponding component of {limit}.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNotComponentWiseGreaterThanException<T>(T value, T limit, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        throw CreateOutOfRange(value, $"Each component must be greater than the corresponding component of {limit}.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNotComponentWiseGreaterThanOrEqualException<T>(T value, T limit, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        throw CreateOutOfRange(value, $"Each component must be at least the corresponding component of {limit}.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNotInClosedRangeException<T>(T value, T minimum, T maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        throw CreateOutOfRange(value, $"Value must be between {minimum} and {maximum}, inclusive.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNotInOpenRangeException<T>(T value, T minimum, T maximum, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        throw CreateOutOfRange(value, $"Value must be between {minimum} and {maximum}, exclusive.", paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowNullException(string? paramName = null) => throw CreateNull(paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalid(string message, string? paramName = null) => throw CreateInvalid(message, paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowInvalid<T>(T _, string message, [CallerArgumentExpression(nameof(_))] string? paramName = null) =>
        throw CreateInvalid(message, paramName);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowOutOfRange<T>(T actualValue, string message,
        [CallerArgumentExpression(nameof(actualValue))] string? paramName = null) =>
        throw CreateOutOfRange(actualValue, message, paramName);

    public static ArgumentException CreateInvalid(string message, string? paramName = null) => new(message, paramName);
    public static ArgumentNullException CreateNull(string? paramName = null) => new(paramName);
    public static ArgumentOutOfRangeException CreateOutOfRange<T>(T actualValue, string message,
        [CallerArgumentExpression(nameof(actualValue))] string? paramName = null) => new(paramName, actualValue, message);
}
