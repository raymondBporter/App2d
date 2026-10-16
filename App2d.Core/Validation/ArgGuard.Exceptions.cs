using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace App2d.Core.Validation;

public static partial class ArgGuard
{
    // These cold helpers allocate a fresh exception only on failure. Never cache exception instances.
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
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static ArgumentOutOfRangeException CreateOutOfRange<T>(T actualValue, string message,
        [CallerArgumentExpression(nameof(actualValue))] string? paramName = null) => new(paramName, actualValue, message);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ArgumentOutOfRangeException CreateRangeException<T>(T value, T minimum, T maximum, RangeBounds bounds, string? paramName)
    {
        var left = bounds is RangeBounds.Closed or RangeBounds.ClosedOpen ? '[' : '(';
        var right = bounds is RangeBounds.Closed or RangeBounds.OpenClosed ? ']' : ')';
        return CreateOutOfRange(value, $"Value must be finite and in {left}{minimum}, {maximum}{right}.", paramName);
    }
}
