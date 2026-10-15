using System.Numerics;

namespace App2d.Core.Mathematics.Progress;

/// <summary>Clamped progress shaping, its inverses, and unclamped maps with analytic derivatives.</summary>
public static class Progress
{
    private const int InverseIterations = 48;

    public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

    /// <summary>Cubic smoothstep, clamped to [0,1], with zero first derivative at both ends.</summary>
    public static float SmoothStep(float value) => (float)SmoothStepMap.Sample(Clamp01(value));

    /// <summary>Quintic smootherstep, clamped to [0,1], with zero first and second derivatives at both ends.</summary>
    public static float SmootherStep(float value) => (float)SmootherStepMap.Sample(Clamp01(value));

    /// <summary>Septic smoothstep, clamped to [0,1], with zero first through third derivatives at both ends.</summary>
    public static float SmoothestStep(float value) => (float)SmoothestStepMap.Sample(Clamp01(value));

    /// <summary>Unclamped cubic smoothstep packaged with its derivative.</summary>
    public static ProgressMap SmoothStepMap { get; } = new(
        SmoothStepUnclamped,
        x => 6d * x * (1d - x));

    /// <summary>The cubic polynomial in the caller's floating-point precision, without clamping.</summary>
    public static T SmoothStepUnclamped<T>(T value) where T : IFloatingPointIeee754<T> =>
        value * value * (T.CreateChecked(3) - T.CreateChecked(2) * value);

    /// <summary>Unclamped quintic smoothstep packaged with its derivative.</summary>
    public static ProgressMap SmootherStepMap { get; } = new(
        SmootherStepUnclamped,
        x => 30d * x * x * (1d - x) * (1d - x));

    public static T SmootherStepUnclamped<T>(T value) where T : IFloatingPointIeee754<T> =>
        value * value * value * (value * (value * T.CreateChecked(6) - T.CreateChecked(15)) + T.CreateChecked(10));

    /// <summary>Unclamped septic smoothstep packaged with its derivative.</summary>
    public static ProgressMap SmoothestStepMap { get; } = new(
        SmoothestStepUnclamped,
        x => 140d * x * x * x * (1d - x) * (1d - x) * (1d - x));

    public static T SmoothestStepUnclamped<T>(T value) where T : IFloatingPointIeee754<T> =>
        value * value * value * value *
        (T.CreateChecked(35) + value * (-T.CreateChecked(84) + value * (T.CreateChecked(70) - T.CreateChecked(20) * value)));

    /// <summary>Analytic inverse of the cubic smoothstep on [0,1].</summary>
    public static float InverseSmoothStep(float value)
    {
        value = Clamp01(value);
        return (float)(0.5d - Math.Sin(Math.Asin(1d - 2d * value) / 3d));
    }

    public static float InverseSmootherStep(float value) => InvertMonotonicStep(value, SmootherStepMap);
    public static float InverseSmoothestStep(float value) => InvertMonotonicStep(value, SmoothestStepMap);

    private static float InvertMonotonicStep(float value, ProgressMap step)
    {
        value = Clamp01(value);
        if (value is 0f or 1f) return value;
        var lower = 0d;
        var upper = 1d;
        for (var iteration = 0; iteration < InverseIterations; iteration++)
        {
            var middle = (lower + upper) * 0.5d;
            if (step.Sample(middle) < value) lower = middle;
            else upper = middle;
        }
        return (float)((lower + upper) * 0.5d);
    }
}
