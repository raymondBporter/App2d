using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Validation;

public static partial class ArgGuard
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Matrix3x2 Finite(Matrix3x2 value, [CallerArgumentExpression(nameof(value))] string? paramName = null) =>
        NumericValidation.IsFinite(value) ? value : throw CreateOutOfRange(value, "Every matrix entry must be finite.", paramName);
}
