using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Validation;

namespace App2d.Core.Physics.Motion;

/// <summary>Local-space translation with independent travel, easing, and repetition. Period includes both legs of a round trip.</summary>
public sealed record CurveMotionDefinition2D
{
    public ICurve2D Curve { get; init; } = null!;
    public double PeriodSeconds { get; init; } = 4d;
    public bool Repeat { get; init; } = true;
    public ProgressTravel Travel { get; init; } = ProgressTravel.PingPong;
    public EasingCurve Ease { get; init; } = EasingCurve.Smooth;
    public EasingDirection EaseDirection { get; init; } = EasingDirection.In;

    public CurveMotion2D CreateMotion(Vector2f origin = default)
    {
        Ensure.ThrowIfNull(Curve);
        Curve.Validate();
        var easing = Easingd.FromCurve(Ease, EaseDirection);
        Ensure.ThrowIf(easing.IsContinuous != true, "Motion paths require continuous easing.", nameof(Ease));
        return new(Curve, PeriodSeconds, ProgressMapd.FromTravel(Travel).Then(easing), origin, Repeat);
    }

    public void Validate() => _ = CreateMotion();
}
