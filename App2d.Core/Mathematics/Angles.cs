using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>Angle arithmetic in radians, independent of coordinates or transforms.</summary>
public static class Angles
{
    /// <summary>
    /// Removes whole turns using IEEE remainder, producing [-PI, PI]. Half-turn ties can have either sign;
    /// this preserves the remainder convention rather than choosing a side at the seam. Nonfinite input yields NaN.
    /// </summary>
    public static TReal WrapSigned<TReal>(TReal radians) where TReal : IFloatingPointIeee754<TReal> =>
        TReal.Ieee754Remainder(radians, TReal.Tau);
}
