using App2d.Core.Mathematics.Progress;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>Value interpolation and range-to-progress conversion. Easing and clamping are explicit.</summary>
public static class Interpolation
{
    /// <summary>Unclamped linear interpolation.</summary>
    public static float Lerp(float start, float end, float amount) => start + amount * (end - start);

    /// <summary>Interpolates after mapping progress through a function.</summary>
    public static float Lerp(float start, float end, float progress, Func<float, float> progressMapping)
    {
        ArgGuard.ThrowIfNull(progressMapping);
        return Lerp(start, end, progressMapping(progress));
    }

    /// <summary>Interpolates through a progress map, preserving intentional overshoot.</summary>
    public static float Lerp(float start, float end, double progress, IProgressMap progressMapping)
    {
        ArgGuard.ThrowIfNull(progressMapping);
        return Lerp(start, end, (float)progressMapping.Sample(progress));
    }

    /// <summary>Unclamped linear interpolation between vectors.</summary>
    public static Vector2 Lerp(Vector2 start, Vector2 end, float amount) => start + amount * (end - start);

    public static Vector2 Lerp(Vector2 start, Vector2 end, float progress, Func<float, float> progressMapping)
    {
        ArgGuard.ThrowIfNull(progressMapping);
        return Lerp(start, end, progressMapping(progress));
    }

    public static Vector2 Lerp(Vector2 start, Vector2 end, double progress, IProgressMap progressMapping)
    {
        ArgGuard.ThrowIfNull(progressMapping);
        return Lerp(start, end, (float)progressMapping.Sample(progress));
    }

    /// <summary>Unclamped progress from start to end. A zero-length range returns zero.</summary>
    public static float InverseLerp(float start, float end, float value) => start == end ? 0f : (value - start) / (end - start);

    public static float InverseLerpClamped(float start, float end, float value) => App2d.Core.Mathematics.Progress.Progress.Clamp01(InverseLerp(start, end, value));
}
