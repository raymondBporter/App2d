using App2d.Core.Geometry.Distance;

namespace App2d.Core.Geometry.Curves;

/// <summary>A quadratic Bezier curve over normalized progress [0, 1].</summary>
public readonly partial record struct QuadraticBezier2D : ICurve2D, ITwiceDifferentiableCurve<Vector2f, float>, IControlPointCurve<Vector2f, ICurve2D>, IPolynomialCurve2D, IInterpolatable<QuadraticBezier2D, float>
{
    public GeometryKind Kind => GeometryKind.QuadraticBezier;
    public Interval<float> Domain => Interval.UnitInterval<float>();

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

    public Vector2f SecondDerivative(float amount) => BezierMath.QuadraticSecondDerivative<Vector2f, float>(Start, Control, End);

    public int ControlPointCount => 3;
    public Vector2f GetControlPoint(int index) => index switch { 0 => Start, 1 => Control, 2 => End, _ => throw new ArgumentOutOfRangeException(nameof(index)) };
    public ICurve2D WithControlPoints(ReadOnlySpan<Vector2f> points) => points.Length == 3 ? this with { Start = points[0], Control = points[1], End = points[2] } : throw new InvalidDataException("Curve point count must match its definition.");

    public PolynomialSpans2D GetPolynomialSpans() => new(Start, Control, End);

    public float Distance(Vector2f point) =>
        (float)Math.Sqrt(CurveDistance2D.QuadraticDistanceSquared(point.ToVector2d(), Start.ToVector2d(), Control.ToVector2d(), End.ToVector2d(), 0, 1));
}
