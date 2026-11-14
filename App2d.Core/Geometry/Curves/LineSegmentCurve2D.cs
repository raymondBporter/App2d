using App2d.Core.Geometry.Primitives;
using System.Numerics;

namespace App2d.Core.Geometry.Curves;

/// <summary>A finite line segment parameterized from start to end over [0, 1].</summary>
public readonly partial record struct LineSegmentCurve2D : ICurve2D, ISegment<LineSegmentCurve2D, Vector2f>
{
    public LineSegmentCurve2D(Vector2f start, Vector2f end)
    {
        Start = start;
        End = end;
    }

    public Vector2f Start { get; init; }
    public Vector2f End { get; init; }
    public static LineSegmentCurve2D Create(Vector2f start, Vector2f end) => Builder.Create(start, end);
    public Vector2f Evaluate(float amount) => this.PointAt(amount);
    public Vector2f EvaluateDerivative(float amount) => End - Start;
    public float Distance(Vector2f point) => Distance2D.DistanceToSegment(point, Start, End);
}
