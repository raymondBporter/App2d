using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Timing;
using App2d.Core.Validation;

namespace App2d.Core.Physics.Motion;

/// <summary>
/// Repeating or one-shot translation composed from a geometric curve and a progress map.
/// Progress outside [0, 1] extends the curve along its endpoint tangents, within a loose sanity limit.
/// </summary>
public sealed class CurveMotion2D : IKinematicMotion2D
{
    private const double ProgressOvershootAllowance = .5d;

    public CurveMotion2D(ICurve2D curve, double periodSeconds, ProgressMapd progress, Vector2f origin = default, bool repeat = true)
    {
        Curve = Ensure.RequireNotNull(curve);
        Progress = Ensure.RequireNotNull(progress);
        Ensure.ThrowIf(progress.IsContinuous == false, "Motion paths require a continuous progress map.", nameof(progress));
        PeriodSeconds = Ensure.Positive(periodSeconds);
        Ensure.Finite(origin);
        Origin = origin;
        Repeat = repeat;

        var beginning = EvaluateProgress(CheckedProgress(Progress.Sample(0d)));
        var ending = EvaluateProgress(CheckedProgress(Progress.Sample(1d)));
        Ensure.Finite(beginning);
        Ensure.Finite(ending);
        var tolerance = 0.001f + MathF.Max(beginning.Length(), ending.Length()) * 0.000001f;
        Ensure.ThrowIf(repeat && Vector2f.Distance(beginning, ending) > tolerance,
            "A repeating motion must return to its starting position.", nameof(progress));
    }

    public ICurve2D Curve { get; }
    public ProgressMapd Progress { get; }
    public double PeriodSeconds { get; }
    public Vector2f Origin { get; }
    public bool Repeat { get; }

    public Vector2f Position(double seconds) => Origin + EvaluateProgress(CheckedProgress(Progress.Sample(Phase(seconds))));

    public Vector2f Velocity(double seconds)
    {
        var phase = Phase(seconds);
        if (!Repeat && seconds >= PeriodSeconds) return Vector2f.Zero;
        var progress = CheckedProgress(Progress.Sample(phase));
        var derivative = Progress.Derivative(phase);
        Ensure.Finite(derivative, nameof(Progress));
        return Curve.EvaluateDerivative(Clamping.Clamp01(progress)) * (float)(derivative / PeriodSeconds);
    }

    private Vector2f EvaluateProgress(float progress)
    {
        if (progress is >= 0f and <= 1f) return Curve.Evaluate(progress);
        var endpoint = progress < 0f ? 0f : 1f;
        return Curve.Evaluate(endpoint) + Curve.EvaluateDerivative(endpoint) * (progress - endpoint);
    }

    private double Phase(double seconds)
    {
        return PlaybackClock.SampleTime(seconds, PeriodSeconds, Repeat ? PlaybackEndMode.Loop : PlaybackEndMode.Hold) / PeriodSeconds;
    }

    private static float CheckedProgress(double progress)
    {
        Ensure.InRange(progress, -ProgressOvershootAllowance, 1d + ProgressOvershootAllowance);
        return (float)progress;
    }
}
