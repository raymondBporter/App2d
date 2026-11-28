using App2d.Core.Mathematics.Progress;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics;

public static partial class Interpolation
{
    public static Interpolator<TValue, TReal> Default<TValue, TReal>()
        where TValue : IInterpolatable<TValue, TReal>
        where TReal : IFloatingPointIeee754<TReal>
    {
        return new(static (start, end, amount) => TValue.Lerp(start, end, amount), InterpolationKind.Default);
    }

    public static Interpolator<TReal, TReal> Linear<TReal>()
        where TReal : IFloatingPointIeee754<TReal>
    {
        return new(static (start, end, amount) => Interval.Lerp(start, end, amount), InterpolationKind.Linear);
    }

    /// <summary>Holds the first value regardless of time. Timeline sampling still returns exact keys directly.</summary>
    public static Interpolator<TValue, TReal> Hold<TValue, TReal>() where TReal : IFloatingPointIeee754<TReal>
    {
        return new(static (start, _, _) => start, InterpolationKind.Hold);
    }

    /// <summary>Takes the signed shortest angular difference; half-turn ties follow IEEE remainder.</summary>
    public static Interpolator<TReal, TReal> ShortestAngle<TReal>() where TReal : IFloatingPointIeee754<TReal>
    {
        return new(static (start, end, amount) => start + Angles.WrapSigned(end - start) * amount, InterpolationKind.ShortestAngle);
    }

    /// <summary>Relative handles shape progress; absolute handles specify actual values, including excursions between equal endpoints.</summary>
    public static Interpolator<TReal, TReal> Bezier<TReal>(TReal firstTime, TReal firstValue, TReal secondTime, TReal secondValue,
        bool absolute = false) where TReal : IFloatingPointIeee754<TReal>
    {
        Ensure.InRange(firstTime, TReal.Zero, TReal.One);
        Ensure.InRange(secondTime, TReal.Zero, TReal.One);
        Ensure.Finite(firstValue);
        Ensure.Finite(secondValue);
        if (!absolute)
            return Linear<TReal>().WithProgress(Easing<TReal>.TimedBezier(firstTime, firstValue, secondTime, secondValue));
        var function = new BezierInterpolation<TReal>(firstTime, firstValue, secondTime, secondValue);
        return new(function.Evaluate);
    }

    /// <summary>
    /// Interpolates Cartesian vectors in polar coordinates about the origin. Defaults to linear radius and shortest angle.
    /// At the origin, uses the other endpoint's heading. Signed radius overshoot is preserved in the Cartesian result.
    /// </summary>
    public static Interpolator<TVector, TReal> Polar<TVector, TReal>(Interpolator<TReal, TReal>? radius = null,
        Interpolator<TReal, TReal>? angle = null)
        where TVector : unmanaged, IVector2<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal>
    {
        var function = new PolarInterpolation<TVector, TReal>(radius ?? Linear<TReal>(), angle ?? ShortestAngle<TReal>());
        return new(function.Evaluate);
    }
}
