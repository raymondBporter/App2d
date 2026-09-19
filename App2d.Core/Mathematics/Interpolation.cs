using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>
/// Normalized progress mappings and helpers for applying them to interpolation.
/// Step functions clamp their input to [0, 1]; plain Lerp and InverseLerp do not.
/// </summary>
public static class Interpolation
{
    private const int InverseIterations = 48;

    /// <summary>Clamps linear progress to [0, 1].</summary>
    public static float LinearStep(float value) => Math.Clamp(value, 0f, 1f);

    /// <summary>Cubic smoothstep with zero first derivative at both ends.</summary>
    public static float SmoothStep(float value)
    {
        var progress = (double)LinearStep(value);
        return (float)(progress * progress * (3d - 2d * progress));
    }

    /// <summary>Quintic smootherstep with zero first and second derivatives at both ends.</summary>
    public static float SmootherStep(float value)
    {
        var progress = (double)LinearStep(value);
        return (float)SmootherStepCore(progress);
    }

    /// <summary>Septic smoothstep with zero first through third derivatives at both ends.</summary>
    public static float SmoothestStep(float value)
    {
        var progress = (double)LinearStep(value);
        return (float)SmoothestStepCore(progress);
    }

    /// <summary>Analytic inverse of <see cref="SmoothStep"/> on [0, 1].</summary>
    public static float InverseSmoothStep(float value)
    {
        value = LinearStep(value);
        return (float)(0.5d - Math.Sin(Math.Asin(1d - 2d * value) / 3d));
    }

    /// <summary>Numerical inverse of <see cref="SmootherStep"/> on [0, 1].</summary>
    public static float InverseSmootherStep(float value) =>
        InvertMonotonicStep(value, SmootherStepCore);

    /// <summary>Numerical inverse of <see cref="SmoothestStep"/> on [0, 1].</summary>
    public static float InverseSmoothestStep(float value) =>
        InvertMonotonicStep(value, SmoothestStepCore);

    /// <summary>Unclamped linear interpolation.</summary>
    public static float Lerp(float start, float end, float amount) =>
        start + amount * (end - start);

    /// <summary>Interpolates after mapping progress through a step or easing function.</summary>
    public static float Lerp(
        float start,
        float end,
        float progress,
        Func<float, float> progressMapping)
    {
        ArgumentNullException.ThrowIfNull(progressMapping);
        return Lerp(start, end, progressMapping(progress));
    }

    /// <summary>Unclamped linear interpolation between vectors.</summary>
    public static Vector2 Lerp(Vector2 start, Vector2 end, float amount) =>
        start + amount * (end - start);

    /// <summary>Interpolates between vectors after mapping normalized progress.</summary>
    public static Vector2 Lerp(
        Vector2 start,
        Vector2 end,
        float progress,
        Func<float, float> progressMapping)
    {
        ArgumentNullException.ThrowIfNull(progressMapping);
        return Lerp(start, end, progressMapping(progress));
    }

    /// <summary>
    /// Returns the unclamped progress of <paramref name="value"/> from start to end.
    /// A zero-length range returns zero.
    /// </summary>
    public static float InverseLerp(float start, float end, float value) =>
        start == end ? 0f : (value - start) / (end - start);

    /// <summary>Returns progress from start to end clamped to [0, 1].</summary>
    public static float InverseLerpClamped(float start, float end, float value) =>
        LinearStep(InverseLerp(start, end, value));

    private static float InvertMonotonicStep(float value, Func<double, double> step)
    {
        value = LinearStep(value);
        if (value is 0f or 1f)
            return value;

        var lower = 0d;
        var upper = 1d;
        for (var iteration = 0; iteration < InverseIterations; iteration++)
        {
            var middle = (lower + upper) * 0.5f;
            if (step(middle) < value)
                lower = middle;
            else
                upper = middle;
        }
        return (float)((lower + upper) * 0.5d);
    }

    private static double SmootherStepCore(double value) =>
        value * value * value * (value * (value * 6d - 15d) + 10d);

    private static double SmoothestStepCore(double value) =>
        value * value * value * value *
        (35d + value * (-84d + value * (70d - 20d * value)));
}
