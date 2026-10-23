using App2d.Core.Geometry.Primitives;
using System.Numerics;

namespace App2d.Core.Geometry.Curves;

/// <summary>A finite line segment parameterized from start to end over [0, 1].</summary>
public readonly partial record struct LineSegmentCurve2D : ICurve2D, ISegment<LineSegmentCurve2D, Vector2>
{
    public string Kind => CurveKinds2D.Line;

    public LineSegmentCurve2D(Vector2 start, Vector2 end)
    {
        Start = start;
        End = end;
    }

    public Vector2 Start { get; init; }
    public Vector2 End { get; init; }
    public static LineSegmentCurve2D Create(Vector2 start, Vector2 end) => Builder.Create(start, end);
    public Vector2 Evaluate(float amount) => this.PointAt(amount);
    public Vector2 EvaluateDerivative(float amount) => End - Start;
    public float Distance(Vector2 point) => Distance2D.DistanceToSegment(point, Start, End);
}
