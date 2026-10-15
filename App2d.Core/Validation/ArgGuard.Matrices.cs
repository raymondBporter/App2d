using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Validation;

public static partial class ArgGuard
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ThrowIfNotFinite(Matrix3x2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (!NumericValidation.IsFinite(value))
        {
            ThrowNotFiniteException(value, paramName);
        }
    }
}
