using App2d.Core.Validation;
using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Core.Curves;

/// <summary>A finite line segment parameterized from start to end over [0, 1].</summary>
public readonly record struct LineSegmentCurve2D : ICurve2D
{
    public LineSegmentCurve2D(Vector2 start, Vector2 end)
    {
        ArgGuard.ThrowIfNotFinite(start);
        ArgGuard.ThrowIfNotFinite(end);
        Start = start;
        End = end;
    }

    public Vector2 Start { get; }
    public Vector2 End { get; }
    public Vector2 Evaluate(float amount) => Vector2.Lerp(Start, End, Math.Clamp(amount, 0f, 1f));
    public Vector2 EvaluateDerivative(float amount) => End - Start;
    public float Distance(Vector2 point) => Distance2D.DistanceToSegment(point, Start, End);
}
