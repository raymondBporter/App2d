using App2d.Core.Curves;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Gameplay.World;

/// <summary>A line segment played forward and backward, with optional easing on each leg.</summary>
public sealed class PingPongMotion2D : IKinematicMotion2D
{
    private readonly CurveMotion2D _motion;

    public PingPongMotion2D(Vector2 start, Vector2 travel, float speed, IProgressMap? easing = null)
    {
        ArgGuard.ThrowIfNotFinite(start);
        ArgGuard.ThrowIfNotFinite(travel);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(speed);
        var length = travel.Length();
        ArgGuard.ThrowIf(!float.IsFinite(length) || length <= 0f,
            "Travel must have a finite, non-zero length.", nameof(travel));
        Start = start;
        End = start + travel;
        ArgGuard.ThrowIfNotFinite(End);
        easing ??= Easing.Linear;
        ArgGuard.ThrowIf(Math.Abs(easing.Sample(0d)) > 0.000001d ||
            Math.Abs(easing.Sample(1d) - 1d) > 0.000001d,
            "Easing must map the endpoints to 0 and 1.", nameof(easing));
        var progress = ProgressMaps.Compose(ProgressMaps.PingPong, easing);
        _motion = new CurveMotion2D(new LineSegmentCurve2D(Start, End),
            2d * length / speed, progress);
    }

    public Vector2 Start { get; }
    public Vector2 End { get; }

    public Vector2 Position(double seconds) => _motion.Position(seconds);
    public Vector2 Velocity(double seconds) => _motion.Velocity(seconds);
}
