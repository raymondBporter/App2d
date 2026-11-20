using App2d.Core.Mathematics.Progress;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>Value interpolation and range-to-progress conversion. Easing and clamping are explicit.</summary>
public static class Interpolation
{
    /// <summary>Unclamped linear interpolation.</summary>
    public static TReal Lerp<TReal>(TReal start, TReal end, TReal amount) where TReal : IFloatingPointIeee754<TReal> =>
        start + amount * (end - start);

    /// <summary>Interpolates after mapping progress through a function.</summary>
    public static TReal Lerp<TReal>(TReal start, TReal end, TReal progress, Func<TReal, TReal> progressMapping)
        where TReal : IFloatingPointIeee754<TReal>
    {
        Ensure.ThrowIfNull(progressMapping);
        return Lerp(start, end, progressMapping(progress));
    }

    /// <summary>Interpolates through a progress map, preserving intentional overshoot.</summary>
    public static TValue Lerp<TValue, TProgress>(TValue start, TValue end, TProgress progress, ProgressMap<TProgress> progressMapping)
        where TValue : IFloatingPointIeee754<TValue>
        where TProgress : IFloatingPointIeee754<TProgress>
    {
        Ensure.ThrowIfNull(progressMapping);
        return Lerp(start, end, TValue.CreateTruncating(progressMapping.Sample(progress)));
    }

    /// <summary>Unclamped linear interpolation between vectors.</summary>
    public static Vector2f Lerp(Vector2f start, Vector2f end, float amount) => start + amount * (end - start);

    public static Vector2f Lerp(Vector2f start, Vector2f end, float progress, Func<float, float> progressMapping)
    {
        Ensure.ThrowIfNull(progressMapping);
        return Lerp(start, end, progressMapping(progress));
    }

    public static Vector2f Lerp<TProgress>(Vector2f start, Vector2f end, TProgress progress, ProgressMap<TProgress> progressMapping)
        where TProgress : IFloatingPointIeee754<TProgress>
    {
        Ensure.ThrowIfNull(progressMapping);
        return Lerp(start, end, float.CreateTruncating(progressMapping.Sample(progress)));
    }

    /// <summary>Unclamped linear interpolation between double-precision vectors.</summary>
    public static Vector2d Lerp(Vector2d start, Vector2d end, double amount) => start + amount * (end - start);

    public static Vector2d Lerp(Vector2d start, Vector2d end, double progress, Func<double, double> progressMapping)
    {
        Ensure.ThrowIfNull(progressMapping);
        return Lerp(start, end, progressMapping(progress));
    }

    public static Vector2d Lerp<TProgress>(Vector2d start, Vector2d end, TProgress progress, ProgressMap<TProgress> progressMapping)
        where TProgress : IFloatingPointIeee754<TProgress>
    {
        Ensure.ThrowIfNull(progressMapping);
        return Lerp(start, end, double.CreateTruncating(progressMapping.Sample(progress)));
    }

    /// <summary>Unclamped progress from start to end. A zero-length range returns zero.</summary>
    public static TReal InverseLerp<TReal>(TReal start, TReal end, TReal value) where TReal : IFloatingPointIeee754<TReal> =>
        start == end ? TReal.Zero : (value - start) / (end - start);

    public static TReal InverseLerpClamped<TReal>(TReal start, TReal end, TReal value) where TReal : IFloatingPointIeee754<TReal> =>
        Clamping.Clamp01(InverseLerp(start, end, value));
}
