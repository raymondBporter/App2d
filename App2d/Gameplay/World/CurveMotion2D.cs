using App2d.Core.Curves;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Gameplay.World;

/// <summary>Periodic motion composed from a geometric curve and a normalized progress map.</summary>
public sealed class CurveMotion2D : IKinematicMotion2D
{
    public CurveMotion2D(ICurve2D curve, double periodSeconds, IProgressMap progress)
    {
        Curve = ArgGuard.RequireNotNull(curve);
        Progress = ArgGuard.RequireNotNull(progress);
        ArgGuard.ThrowIf(!double.IsFinite(periodSeconds) || periodSeconds <= 0d,
            "Period must be finite and positive.", nameof(periodSeconds));
        PeriodSeconds = periodSeconds;

        var beginning = Curve.Evaluate(CheckedProgress(Progress.Sample(0d)));
        var ending = Curve.Evaluate(CheckedProgress(Progress.Sample(1d)));
        ArgGuard.ThrowIfNotFinite(beginning);
        ArgGuard.ThrowIfNotFinite(ending);
        var tolerance = 0.001f + MathF.Max(beginning.Length(), ending.Length()) * 0.000001f;
        ArgGuard.ThrowIf(Vector2.Distance(beginning, ending) > tolerance,
            "A repeating motion must return to its starting position.", nameof(progress));
    }

    public ICurve2D Curve { get; }
    public IProgressMap Progress { get; }
    public double PeriodSeconds { get; }

    public Vector2 Position(double seconds) => Curve.Evaluate(CheckedProgress(Progress.Sample(Phase(seconds))));

    public Vector2 Velocity(double seconds)
    {
        var phase = Phase(seconds);
        var progress = CheckedProgress(Progress.Sample(phase));
        var derivative = Progress.Derivative(phase);
        ArgGuard.ThrowIf(!double.IsFinite(derivative), "Progress derivative must be finite.", nameof(Progress));
        return Curve.EvaluateDerivative(progress) * (float)(derivative / PeriodSeconds);
    }

    private double Phase(double seconds)
    {
        ArgGuard.ThrowIf(!double.IsFinite(seconds) || seconds < 0d,
            "Time must be finite and non-negative.", nameof(seconds));
        return (seconds % PeriodSeconds) / PeriodSeconds;
    }

    private static float CheckedProgress(double progress)
    {
        ArgGuard.ThrowIf(!double.IsFinite(progress) || progress < 0d || progress > 1d,
            "Progress must be finite and in [0, 1].", nameof(progress));
        return (float)progress;
    }
}
