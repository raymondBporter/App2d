using App2d.Core.Mathematics.Progress;
using App2d.Core.Mathematics.Serialization;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics;

public static partial class Interpolation
{
    public static Interpolator<TValue, TReal> Default<TValue, TReal>()
        where TValue : IInterpolatable<TValue, TReal>
        where TReal : IFloatingPointIeee754<TReal> => new(static (start, end, amount) => TValue.Lerp(start, end, amount),
            InterpolatorRecipe<TValue, TReal>.Default);

    public static Interpolator<TReal, TReal> Linear<TReal>() where TReal : IFloatingPointIeee754<TReal> =>
        new(static (start, end, amount) => Interval.Lerp(start, end, amount), InterpolatorRecipe<TReal, TReal>.Linear);

    /// <summary>Holds the first value regardless of time. Timeline sampling still returns exact keys directly.</summary>
    public static Interpolator<TValue, TReal> Hold<TValue, TReal>() where TReal : IFloatingPointIeee754<TReal> =>
        new(static (start, _, _) => start, InterpolatorRecipe<TValue, TReal>.Hold);

    /// <summary>Takes the signed shortest angular difference; half-turn ties follow IEEE remainder.</summary>
    public static Interpolator<TReal, TReal> ShortestAngle<TReal>() where TReal : IFloatingPointIeee754<TReal> =>
        new(static (start, end, amount) => start + Angles.WrapSigned(end - start) * amount, InterpolatorRecipe<TReal, TReal>.ShortestAngle);

    /// <summary>Relative handles shape progress; absolute handles specify actual values, including excursions between equal endpoints.</summary>
    public static Interpolator<TReal, TReal> Bezier<TReal>(TReal firstTime, TReal firstValue, TReal secondTime, TReal secondValue,
        bool absolute = false) where TReal : IFloatingPointIeee754<TReal>
    {
        Ensure.InRange(firstTime, TReal.Zero, TReal.One);
        Ensure.InRange(secondTime, TReal.Zero, TReal.One);
        Ensure.Finite(firstValue);
        Ensure.Finite(secondValue);
        return absolute
            ? new((start, end, time) => Easing<TReal>.TimedBezierValue(start, firstTime, firstValue, secondTime, secondValue, end, time),
                new InterpolatorRecipe<TReal, TReal>.Bezier(firstTime, firstValue, secondTime, secondValue))
            : Linear<TReal>().WithProgress(Easing<TReal>.TimedBezier(firstTime, firstValue, secondTime, secondValue));
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
        var radial = radius ?? Linear<TReal>();
        var angular = angle ?? ShortestAngle<TReal>();
        return new((start, end, time) =>
        {
            var a = TVector.ToPolar(start);
            var b = TVector.ToPolar(end);
            var firstAngle = a.Radius == TReal.Zero ? b.AngleRadians : a.AngleRadians;
            var secondAngle = b.Radius == TReal.Zero ? firstAngle : b.AngleRadians;
            return TVector.Direction(angular.Evaluate(firstAngle, secondAngle, time)) * radial.Evaluate(a.Radius, b.Radius, time);
        }, new InterpolatorRecipe<TVector, TReal>.Polar(radial, angular));
    }
}
