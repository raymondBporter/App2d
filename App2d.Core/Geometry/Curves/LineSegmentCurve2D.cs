using App2d.Core.Geometry.Distance;
using App2d.Core.Geometry.Primitives;

namespace App2d.Core.Geometry.Curves;

/// <summary>A finite line segment parameterized from start to end over [0, 1].</summary>
public readonly partial record struct LineSegmentCurve2D : ICurve2D, ISegment<LineSegmentCurve2D, Vector2f>, IInterpolatable<LineSegmentCurve2D, float>
{
    public GeometryKind Kind => GeometryKind.LineSegment;

    /// <summary>The two ends of the segment, each of which an interpolator can drive on its own.</summary>
    public static class Channels
    {
        public static InterpolationChannel<LineSegmentCurve2D, LineSegmentCurve2D> V => InterpolationChannel<LineSegmentCurve2D, LineSegmentCurve2D>.Whole;
        public static InterpolationChannel<LineSegmentCurve2D, Vector2f> Start { get; } = new(static value => value.Start, static (value, point) => value with { Start = point });
        public static InterpolationChannel<LineSegmentCurve2D, Vector2f> End { get; } = new(static value => value.End, static (value, point) => value with { End = point });
    }

    /// <summary>Moves each end in a straight line.</summary>
    public static LineSegmentCurve2D Lerp(LineSegmentCurve2D start, LineSegmentCurve2D end, float amount) =>
        new(Vector2f.Lerp(start.Start, end.Start, amount), Vector2f.Lerp(start.End, end.End, amount));

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
