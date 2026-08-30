using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace App2d.Core;

public static class StateGuard
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotPositive(
        float value,
        [CallerArgumentExpression(nameof(value))] string? memberName = null)
    {
        if (!float.IsFinite(value) || value <= 0f)
            ThrowCore($"{memberName} must be positive and finite.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfLessThan(
        int value,
        int minimum,
        [CallerArgumentExpression(nameof(value))] string? memberName = null)
    {
        if (value < minimum)
            ThrowCore($"{memberName} must be at least {minimum}.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIf(bool invalidCondition, string message)
    {
        if (invalidCondition)
            ThrowCore(message);
    }

    public static T RequireNotNull<T>([NotNull] T? value, string message)
        where T : class
    {
        if (value is null)
            ThrowCore(message);
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
