using App2d.Core.Mathematics.Progress;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>Value interpolation and range-to-progress conversion. Easing and clamping are explicit.</summary>
public static class Interpolation
{
    /// <summary>Uses the value type's interpolation rule with an already mapped amount.</summary>
    public static TValue Lerp<TValue, TReal>(TValue start, TValue end, TReal amount)
        where TValue : IInterpolatable<TValue, TReal>
        where TReal : IFloatingPointIeee754<TReal> => TValue.Lerp(start, end, amount);

    /// <summary>Maps time once, then uses the value type's interpolation rule. Nothing is clamped.</summary>
    public static TValue Interpolate<TValue, TReal>(this TValue start, TValue end, ProgressMap<TReal> progressMap, TReal time)
        where TValue : IInterpolatable<TValue, TReal>
        where TReal : IFloatingPointIeee754<TReal>
    {
        Ensure.ThrowIfNull(progressMap);
        return TValue.Lerp(start, end, progressMap.Sample(time));
    }

    // Built-in scalars cannot implement our interface. Convert only after evaluating the map.
    public static float Interpolate<TReal>(this float start, float end, ProgressMap<TReal> progressMap, TReal time)
        where TReal : IFloatingPointIeee754<TReal>
    {
        Ensure.ThrowIfNull(progressMap);
        return Lerp(start, end, float.CreateTruncating(progressMap.Sample(time)));
    }

    public static double Interpolate<TReal>(this double start, double end, ProgressMap<TReal> progressMap, TReal time)
        where TReal : IFloatingPointIeee754<TReal>
    {
        Ensure.ThrowIfNull(progressMap);
        return Lerp(start, end, double.CreateTruncating(progressMap.Sample(time)));
    }

    /// <summary>Unclamped linear interpolation.</summary>
    public static TReal Lerp<TReal>(TReal start, TReal end, TReal amount) where TReal : IFloatingPointIeee754<TReal> =>
        Interval.Lerp(start, end, amount);

    /// <summary>Unclamped progress from start to end. A zero-length range returns zero.</summary>
    public static TReal InverseLerp<TReal>(TReal start, TReal end, TReal value) where TReal : IFloatingPointIeee754<TReal> =>
        Interval.InverseLerp(start, end, value);

    public static TReal InverseLerpClamped<TReal>(TReal start, TReal end, TReal value) where TReal : IFloatingPointIeee754<TReal> =>
        Interval.InverseLerpClamped(start, end, value);
}
