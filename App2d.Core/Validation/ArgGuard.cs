using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace App2d.Core;

/// <summary>
/// Argument validation with caller names and rejected values. Numeric predicates live in NumericValidation;
/// throwing/message construction lives in ArgGuard.Exceptions. Explicit NotFiniteOr guards reject infinity/NaN.
/// </summary>
public static partial class ArgGuard
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNull<T>([NotNull] T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value is null) ThrowNullException(paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T RequireNotNull<T>([NotNull] T? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : class
    {
        ThrowIfNull(value, paramName);
        return value;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNullOrWhiteSpace([NotNull] string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        ThrowIfNull(value, paramName);
        if (string.IsNullOrWhiteSpace(value)) ThrowInvalid("Value cannot be empty or whitespace.", paramName);
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
        ThrowIfNegative(minimumLength);
        if (value.Length < minimumLength)
            ThrowOutOfRange(value.Length, $"Collection must contain at least {minimumLength} items.", paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfContainsNull<T>(ReadOnlySpan<T> value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        foreach (var item in value)
            if (item is null) ThrowInvalid("Collection cannot contain null values.", paramName);
    }

    /// <summary>For domain-specific argument rules that do not fit a numeric guard.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIf([DoesNotReturnIf(true)] bool invalidCondition, string message, string? paramName = null)
    {
        if (invalidCondition) ThrowInvalid(message, paramName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfSameReference<T>(T first, T second, string message,
        [CallerArgumentExpression(nameof(first))] string? paramName = null) where T : class =>
        ThrowIf(ReferenceEquals(first, second), message, paramName);

    public static TExpected RequireType<TExpected>(object? value, string message,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        value is TExpected typedValue ? typedValue : throw CreateInvalid(message, paramName);
}
