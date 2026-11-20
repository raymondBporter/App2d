using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Mathematics;

/// <summary>
/// Named constants for generic floating-point code, so formulas say Real&lt;TReal&gt;.Half
/// instead of TReal.One / (TReal.One + TReal.One). The JIT folds each one to a literal.
/// </summary>
public static class Real<TReal> where TReal : IFloatingPointIeee754<TReal>
{
    public static TReal Two { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => TReal.One + TReal.One; }
    public static TReal Half { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => TReal.One / (TReal.One + TReal.One); }

    /// <summary>The gap above one: 2^-23 for float and 2^-52 for double. TReal.Epsilon is instead the smallest subnormal.</summary>
    public static TReal MachineEpsilon { [MethodImpl(MethodImplOptions.AggressiveInlining)] get => TReal.BitIncrement(TReal.One) - TReal.One; }
}
