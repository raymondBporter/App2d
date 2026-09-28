using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace App2d.Core.Validation;

public static partial class ArgGuard
{
    // Previous spellings forward to the canonical finite guards. New callers use NotFiniteOr...
    public static void ThrowIfNegativeOrNotFinite(float value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        ThrowIfNotFiniteOrNegative(value, paramName);

    public static void ThrowIfNotPositiveFiniteValue(float value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        ThrowIfNotFiniteOrNotPositive(value, paramName);

    public static string RequireNotNullOrWhitespace([NotNull] string? value,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        RequireNotNullOrWhiteSpace(value, paramName);
}
