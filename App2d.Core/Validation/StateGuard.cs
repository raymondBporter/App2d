using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace App2d.Core.Validation;

public static class StateGuard
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIf([DoesNotReturnIf(true)] bool invalidCondition, string? message = null,
        [CallerArgumentExpression(nameof(invalidCondition))] string? expression = null)
    {
        if (invalidCondition)
        {
            ThrowCore(message ?? $"Condition must be false: {expression}.");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNot([DoesNotReturnIf(false)] bool condition, string? message = null,
        [CallerArgumentExpression(nameof(condition))] string? expression = null)
    {
        if (!condition)
        {
            ThrowCore(message ?? $"Condition must be true: {expression}.");
        }
    }

    public static T RequireNotNull<T>([NotNull] T? value, string? message = null,
        [CallerArgumentExpression(nameof(value))] string? expression = null)
        where T : class
    {
        if (value is null)
        {
            ThrowCore(message ?? $"{expression} must not be null.");
        }

        return value;
    }

    [DoesNotReturn]
    public static void Throw(string message) =>
        ThrowCore(message);

    public static InvalidOperationException Create(string message) => new(message);

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowCore(string message) =>
        throw new InvalidOperationException(message);
}
