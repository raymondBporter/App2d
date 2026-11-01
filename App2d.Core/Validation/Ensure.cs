using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace App2d.Core.Validation;

/// <summary>
/// Shared validation for inputs and live values. Numeric rules live in NumericValidation;
/// checks return the validated value for use in initializers.
/// </summary>
public static partial class Ensure
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull<T>([NotNull] T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value is null)
        {
            ThrowNullException(paramName);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T RequireNotNull<T>([NotNull] T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null, string? message = null) where T : class
    {
        if (value is null) throw new ArgumentNullException(paramName, message);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNullOrWhiteSpace([NotNull] string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ThrowIfNull(value, paramName);
        if (string.IsNullOrWhiteSpace(value))
        {
            ThrowInvalid("Value cannot be empty or whitespace.", paramName);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string RequireNotNullOrWhiteSpace([NotNull] string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ThrowIfNullOrWhiteSpace(value, paramName);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfTooShort<T>(ReadOnlySpan<T> value, int minimumLength,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        NonNegative(minimumLength);
        if (value.Length < minimumLength)
        {
            ThrowOutOfRange(value.Length, $"Collection must contain at least {minimumLength} items.", paramName);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfContainsNull<T>(ReadOnlySpan<T> value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        foreach (var item in value)
        {
            if (item is null)
            {
                ThrowInvalid("Collection cannot contain null values.", paramName);
            }
        }
    }

    /// <summary>For domain-specific argument rules that do not fit a numeric guard.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIf([DoesNotReturnIf(true)] bool invalidCondition, string? message = null, string? paramName = null,
        [CallerArgumentExpression(nameof(invalidCondition))] string? expression = null)
    {
        if (invalidCondition)
        {
            ThrowInvalid(message ?? $"Condition must be false: {expression}.", paramName);
        }
    }

    /// <summary>Requires a custom condition, capturing its source expression when no message is supplied.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNot([DoesNotReturnIf(false)] bool condition, string? message = null, string? paramName = null,
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition)
        {
            ThrowInvalid(message ?? $"Condition must be true: {expression}.", paramName);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfSameReference<T>(T first, T second, string message,
        [CallerArgumentExpression(nameof(first))] string? paramName = null) where T : class =>
        ThrowIf(ReferenceEquals(first, second), message, paramName);

    public static TExpected RequireType<TExpected>(object? value, string message,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        value is TExpected typedValue ? typedValue : throw CreateInvalid(message, paramName);
}
