using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>Angle arithmetic and unit conversions, independent of coordinates or transforms.</summary>
public static class Angles
{
    /// <summary>One degree expressed in radians, in single precision.</summary>
    public const float RadiansPerDegree = MathF.PI / 180f;

    // Smaller half-angle sines make the pivot inferred from two endpoints ill-conditioned.
    internal const double MinimumHalfAngleSine = 1e-6;

    public static TReal DegreesToRadians<TReal>(TReal degrees) where TReal : ITrigonometricFunctions<TReal> =>
        TReal.DegreesToRadians(degrees);

    public static TReal RadiansToDegrees<TReal>(TReal radians) where TReal : ITrigonometricFunctions<TReal> =>
        TReal.RadiansToDegrees(radians);

    /// <summary>Wraps radians to a nonnegative angle. Tiny negative remainders can round up to one full turn.</summary>
    public static TReal WrapPositive<TReal>(TReal radians) where TReal : IFloatingPointIeee754<TReal>
    {
        var remainder = radians % TReal.Tau;
        return remainder < TReal.Zero ? remainder + TReal.Tau : remainder;
    }

    /// <summary>Computes sine and cosine, returning exactly (0, 1) at zero and one full turn.</summary>
    public static (TReal Sin, TReal Cos) SinCosAtTurnEndpoint<TReal>(TReal radians) where TReal : IFloatingPointIeee754<TReal> =>
        radians == TReal.Zero || radians == TReal.Tau ? (TReal.Zero, TReal.One) : TReal.SinCos(radians);

    /// <summary>
    /// Removes whole turns using IEEE remainder, producing [-PI, PI]. Half-turn ties can have either sign;
    /// this preserves the remainder convention rather than choosing a side at the seam. Nonfinite input yields NaN.
    /// </summary>
    public static TReal WrapSigned<TReal>(TReal radians) where TReal : IFloatingPointIeee754<TReal> =>
        TReal.Ieee754Remainder(radians, TReal.Tau);
}
