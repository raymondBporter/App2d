using App2d.Core.Geometry.Distance;
using App2d.Core.Mathematics.Curves;
using App2d.Core.Mathematics.RootFinding;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Curves;

/// <summary>A cubic Bezier curve with normalized parameter range [0, 1].</summary>
public readonly partial record struct CubicBezier2D : ICurve2D, ITwiceDifferentiableCurve<Vector2f, float>, IControlPointCurve<Vector2f, ICurve2D>, IPolynomialCurve2D
{
    public GeometryKind Kind => GeometryKind.CubicBezier;
    public Interval<float> Domain => Interval.UnitInterval<float>();

    public CubicBezier2D(Vector2f start, Vector2f control1, Vector2f control2, Vector2f end)
    {
        Start = start;
        Control1 = control1;
        Control2 = control2;
        End = end;
    }

    public readonly Vector2f Start { get; init; }
    public readonly Vector2f Control1 { get; init; }
    public readonly Vector2f Control2 { get; init; }
    public readonly Vector2f End { get; init; }

    /// <summary>Creates a cubic from endpoints and tangents with respect to its normalized parameter.</summary>
    public static CubicBezier2D FromHermite(Vector2f start, Vector2f end, Vector2f startTangent, Vector2f endTangent) =>
        From(CubicBezier<Vector2f, float>.FromHermite(start, end, startTangent, endTangent)).Validated();

    // The subdivision math is written once, on the generic curve.
    private CubicBezier<Vector2f, float> Generic => new(Start, Control1, Control2, End);
    private static CubicBezier2D From(CubicBezier<Vector2f, float> curve) => new(curve.Start, curve.Control1, curve.Control2, curve.End);

    public Vector2f Evaluate(float amount) =>
        BezierMath.Cubic(Start, Control1, Control2, End, amount);

    public Vector2f Derivative(float amount) =>
        BezierMath.CubicDerivative(Start, Control1, Control2, End, amount);

    /// <summary>Splits at a finite parameter in [0, 1], preserving the exact curve on each side.</summary>
    public (CubicBezier2D Left, CubicBezier2D Right) Split(float amount)
    {
        Ensure.InRange(amount, 0f, 1f);
        var (left, right) = Generic.Split(amount);
        return (From(left), From(right));
    }

    /// <summary>Restricts the curve to ordered parameters in [0, 1], then maps that interval to [0, 1]. Equal parameters produce a point.</summary>
    public CubicBezier2D Slice(float from, float to)
    {
        Ensure.NonNegative(from);
        Ensure.LessThanOrEqual(to, 1);
        Ensure.LessThanOrEqual(from, to);
        return from == 0 && to == 1 ? this : From(Generic.Slice(from, to));
    }

    /// <summary>Writes the sorted, distinct parameters where the curve has the given X coordinate. The curve may cross it up to three times.</summary>
    /// <param name="x">The finite coordinate to find.</param>
    /// <param name="parameters">Storage for at least three parameters; unused entries are untouched.</param>
    /// <returns>The number of intersections in [0, 1], including endpoints and tangencies.</returns>
    /// <exception cref="InvalidOperationException">The entire curve has this X coordinate, so infinitely many parameters match.</exception>
    public int FindParametersAtX(float x, Span<double> parameters)
    {
        Ensure.Finite(x);
        Ensure.ThrowIfTooShort(parameters, 3);
        var start = (double)Start.X;
        var first = Control1.X - start;
        var second = (double)Control2.X - Control1.X;
        var third = (double)End.X - Control2.X;
        if (start == x && first == 0 && second == 0 && third == 0)
        {
            throw new InvalidOperationException("Every parameter on this curve has the requested X coordinate.");
        }
        Span<double> coefficients = [start - x, 3 * first, 3 * (second - first), third - 2 * second + first];
        return PolynomialRoots.FindRealRoots(coefficients, 0, 1, parameters);
    }

    /// <summary>Finds the parameter for a unique X intersection, as used by monotonic time/value curves.</summary>
    /// <exception cref="InvalidOperationException">There is no intersection, or more than one parameter matches.</exception>
    public float ParameterAtX(float x)
    {
        Ensure.Finite(x);
        var width = (double)End.X - Start.X;
        if (width != 0)
        {
            var first = Interval.InverseLerp((double)Start.X, End.X, Control1.X);
            var second = Interval.InverseLerp((double)Start.X, End.X, Control2.X);
            var target = Interval.InverseLerp((double)Start.X, End.X, x);
            // Handles within the endpoint range guarantee monotonic X, even when the handles cross.
            if (first is >= 0 and <= 1 && second is >= 0 and <= 1 && target is >= 0 and <= 1)
            {
                if (target is 0 or 1)
                {
                    return (float)target;
                }
                // The standard control times make X(u) linear; these common presets need no inverse solve.
                if (Control1.X == Interval.Lerp(Start.X, End.X, 1f / 3f) && Control2.X == Interval.Lerp(Start.X, End.X, 2f / 3f))
                    return (float)target;
                // The normalized X coordinate is a scalar cubic Bezier; invert it as a curve.
                if (BracketedNewton.TrySolve(new CubicBezier<double, double>(0, first, second, 1), target, 64, out var amount))
                {
                    return (float)amount;
                }
            }
        }
        // Use the full solver for nonmonotonic curves and difficult, nearly flat time coordinates.
        Span<double> parameters = stackalloc double[3];
        var count = FindParametersAtX(x, parameters);
        return count == 1 ? (float)parameters[0]
            : throw new InvalidOperationException("The curve must have exactly one parameter at the requested X coordinate.");
    }

    public Vector2f SecondDerivative(float amount) => BezierMath.CubicSecondDerivative(Start, Control1, Control2, End, amount);

    public int ControlPointCount => 4;
    public Vector2f GetControlPoint(int index) => index switch { 0 => Start, 1 => Control1, 2 => Control2, 3 => End, _ => throw new ArgumentOutOfRangeException(nameof(index)) };
    public ICurve2D WithControlPoints(ReadOnlySpan<Vector2f> points) => points.Length == 4 ? this with { Start = points[0], Control1 = points[1], Control2 = points[2], End = points[3] } : throw new InvalidDataException("Curve point count must match its definition.");

    public PolynomialSpans2D GetPolynomialSpans() => new(Start, Control1, Control2, End);

    public float Distance(Vector2f point) =>
        (float)Math.Sqrt(CurveDistance2D.CubicDistanceSquared(point.ToVector2d(), Start.ToVector2d(), Control1.ToVector2d(), Control2.ToVector2d(), End.ToVector2d(), 0, 1));
}
