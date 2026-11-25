using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Mathematics;

/// <summary>
/// Constants and unit-interval clamping for floating-point code, so formulas say Real&lt;TReal&gt;.Half
/// instead of TReal.One / (TReal.One + TReal.One). The JIT folds each one to a literal.
/// </summary>
public static class Real<TReal> where TReal : IFloatingPointIeee754<TReal>
{
    public static TReal One
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => TReal.One;
    }

    public static TReal Two
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => TReal.One + TReal.One;
    }

    public static TReal Half
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => TReal.One / Two;
    }

    /// <summary>The gap above one: 2^-23 for float and 2^-52 for double. TReal.Epsilon is instead the smallest subnormal.</summary>
    public static TReal MachineEpsilon
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => TReal.BitIncrement(TReal.One) - TReal.One;
    }

    /// <summary>Clamps a value to the inclusive range [0, 1].</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TReal Clamp01(TReal value) => TReal.Clamp(value, TReal.Zero, TReal.One);
}
