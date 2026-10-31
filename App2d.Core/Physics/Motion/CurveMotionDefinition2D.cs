using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Physics.Motion;

/// <summary>Local-space translation. Period includes the entire pipeline; one-shot motion holds its end.</summary>
public sealed record CurveMotionDefinition2D
{
    public ICurve2D Curve { get; init; } = null!;
    public double PeriodSeconds { get; init; } = 4d;
    public bool Repeat { get; init; } = true;
    public IReadOnlyList<ProgressMapKind> Progress { get; init; } = [ProgressMapKind.PingPong, ProgressMapKind.Smooth];

    public CurveMotion2D CreateMotion(Vector2 origin = default)
    {
        ArgGuard.ThrowIfNull(Curve);
        Curve.Validate();
        ArgGuard.ThrowIfNull(Progress);
        ArgGuard.ThrowIf(Progress.Any(kind => ProgressMaps.FromKind(kind).IsContinuous != true),
            "Motion paths require continuous progress maps.", nameof(Progress));
        return new(Curve, PeriodSeconds, ProgressMaps.Compose(Progress), origin, Repeat);
    }

    public void Validate() => _ = CreateMotion();
}
