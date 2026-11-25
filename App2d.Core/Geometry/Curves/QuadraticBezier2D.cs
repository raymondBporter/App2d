using App2d.Core.Geometry.Distance;

namespace App2d.Core.Geometry.Curves;

/// <summary>A quadratic Bezier curve over normalized progress [0, 1].</summary>
public readonly partial record struct QuadraticBezier2D : ICurve2D, IInterpolatable<QuadraticBezier2D, float>
{
    public GeometryKind Kind => GeometryKind.QuadraticBezier;

    /// <summary>The three points of the curve, each of which an interpolator can drive on its own.</summary>
    public static class Channels
    {
        public static InterpolationChannel<QuadraticBezier2D, QuadraticBezier2D> V => InterpolationChannel<QuadraticBezier2D, QuadraticBezier2D>.Whole;
        public static InterpolationChannel<QuadraticBezier2D, Vector2f> Start { get; } = new(static value => value.Start, static (value, point) => value with { Start = point });
        public static InterpolationChannel<QuadraticBezier2D, Vector2f> Control { get; } = new(static value => value.Control, static (value, point) => value with { Control = point });
        public static InterpolationChannel<QuadraticBezier2D, Vector2f> End { get; } = new(static value => value.End, static (value, point) => value with { End = point });
    }

    /// <summary>Moves each of the three points in a straight line.</summary>
    public static QuadraticBezier2D Lerp(QuadraticBezier2D start, QuadraticBezier2D end, float amount) =>
        new(Vector2f.Lerp(start.Start, end.Start, amount), Vector2f.Lerp(start.Control, end.Control, amount), Vector2f.Lerp(start.End, end.End, amount));

    public QuadraticBezier2D(Vector2f start, Vector2f control, Vector2f end)
    {
        Start = start;
        Control = control;
        End = end;
    }

    public Vector2f Start { get; init; }
    public Vector2f Control { get; init; }
    public Vector2f End { get; init; }

    public Vector2f Evaluate(float amount) =>
        BezierMath.Quadratic(Start, Control, End, amount);

    public Vector2f Derivative(float amount) =>
        BezierMath.QuadraticDerivative(Start, Control, End, amount);

    public float Distance(Vector2f point) => Distance(point, 0, 1);

    internal float Distance(Vector2f point, double from, double to) =>
        (float)Math.Sqrt(CurveDistance2D.QuadraticDistanceSquared(point.ToVector2d(),
            Start.ToVector2d(), Control.ToVector2d(), End.ToVector2d(), from, to));
}
