using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// A value with an explicit interpolation rule. Generic callers retain the concrete type;
/// implementations decide how their components, angles, or other invariants interpolate.
/// </summary>
public interface IInterpolatable<TSelf, TReal>
    where TSelf : IInterpolatable<TSelf, TReal>
    where TReal : IFloatingPointIeee754<TReal>
{
    /// <summary>
    /// Interpolates using an already mapped amount. Values outside [0, 1] request extrapolation;
    /// the implementation owns any restrictions its value type requires.
    /// </summary>
    static abstract TSelf Lerp(TSelf start, TSelf end, TReal amount);
}
