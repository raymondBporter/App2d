using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Validation;
using App2d.Core.Timing;
using System.Numerics;

namespace App2d.Core.Physics.Motion;

/// <summary>Repeating or one-shot translation composed from a geometric curve and a normalized progress map.</summary>
public sealed class CurveMotion2D : IKinematicMotion2D
{
    public CurveMotion2D(ICurve2D curve, double periodSeconds, IProgressMap progress, Vector2 origin = default, bool repeat = true)
    {
        Curve = ArgGuard.RequireNotNull(curve);
        Progress = ArgGuard.RequireNotNull(progress);
        PeriodSeconds = ArgGuard.Positive(periodSeconds);
        ArgGuard.Finite(origin);
        Origin = origin;
        Repeat = repeat;

        var beginning = Curve.Evaluate(CheckedProgress(Progress.Sample(0d)));
        var ending = Curve.Evaluate(CheckedProgress(Progress.Sample(1d)));
        ArgGuard.Finite(beginning);
        ArgGuard.Finite(ending);
        var tolerance = 0.001f + MathF.Max(beginning.Length(), ending.Length()) * 0.000001f;
        ArgGuard.ThrowIf(repeat && Vector2.Distance(beginning, ending) > tolerance,
            "A repeating motion must return to its starting position.", nameof(progress));
    }

    public ICurve2D Curve { get; }
    public IProgressMap Progress { get; }
    public double PeriodSeconds { get; }
    public Vector2 Origin { get; }
    public bool Repeat { get; }

    public Vector2 Position(double seconds) => Origin + Curve.Evaluate(CheckedProgress(Progress.Sample(Phase(seconds))));

    public Vector2 Velocity(double seconds)
    {
        var phase = Phase(seconds);
        if (!Repeat && seconds >= PeriodSeconds) return Vector2.Zero;
        var progress = CheckedProgress(Progress.Sample(phase));
        var derivative = Progress.Derivative(phase);
        ArgGuard.Finite(derivative, nameof(Progress));
        return Curve.EvaluateDerivative(progress) * (float)(derivative / PeriodSeconds);
    }

    private double Phase(double seconds)
    {
        return PlaybackClock.SampleTime(seconds, PeriodSeconds, Repeat ? PlaybackEndMode.Loop : PlaybackEndMode.Hold) / PeriodSeconds;
    }

    private static float CheckedProgress(double progress)
    {
        ArgGuard.InRange(progress, 0d, 1d);
        return (float)progress;
    }
}
