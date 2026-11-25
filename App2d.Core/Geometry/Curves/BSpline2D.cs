using App2d.Core.Geometry.Distance;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Geometry.Curves;

/// <summary>
/// A clamped uniform B-spline. Cubic is the default; lower degrees are useful
/// for polylines and higher degrees are supported when enough control points exist.
/// </summary>
public readonly partial record struct BSpline2D : ICurve2D, ITwiceDifferentiableCurve<Vector2f, float>, IControlPointCurve<Vector2f, ICurve2D>, IPolynomialCurve2D
{
    public GeometryKind Kind => GeometryKind.BSpline;
    public Interval<float> Domain => Interval.UnitInterval<float>();

    private readonly Vector2f[]? _controlPoints;
    private static readonly ConditionalWeakTable<Vector2f[], Dictionary<int, Prepared>> PreparedCurves = [];

    public BSpline2D(IEnumerable<Vector2f> controlPoints, int degree = 3)
    {
        _controlPoints = controlPoints?.ToArray();
        Degree = degree;
    }

    public int Degree { get; init; } = 3;
    public BSpline2D() { }
    public ReadOnlySpan<Vector2f> ControlPoints { get => _controlPoints; init => _controlPoints = value.ToArray(); }

    private Prepared Data
    {
        get
        {
            var entries = PreparedCurves.GetValue(_controlPoints!, static _ => []);
            lock (entries)
            {
                if (!entries.TryGetValue(Degree, out var data))
                {
                    Validate();
                    var knots = CreateClampedUniformKnots(_controlPoints!.Length, Degree);
                    var first = CreateDerivativeControlPoints(_controlPoints, knots, Degree);
                    // The derivative of a B-spline is a B-spline one degree lower on the inner knots, so the same step applies twice.
                    data = new(knots, first, Degree < 2 ? [] : CreateDerivativeControlPoints(first, knots.AsSpan(1, knots.Length - 2), Degree - 1));
                    entries[Degree] = data;
                }
                return data;
            }
        }
    }
    private sealed record Prepared(float[] Knots, Vector2f[] DerivativePoints, Vector2f[] SecondDerivativePoints)
    {
        internal PolynomialCurveSpan2D[]? PolynomialSpans;
    }

    public Vector2f Evaluate(float amount) => EvaluateDeBoor(_controlPoints, Data.Knots, Degree, amount);
    public Vector2f Derivative(float amount)
    {
        // A degree-1 spline has a constant tangent per span, which would otherwise hide a NaN parameter.
        if (float.IsNaN(amount)) return new Vector2f(float.NaN, float.NaN);
        var data = Data;
        return EvaluateDeBoor(data.DerivativePoints, data.Knots.AsSpan(1, data.Knots.Length - 2), Degree - 1, amount);
    }
    public Vector2f SecondDerivative(float amount)
    {
        var data = Data;
        return Degree < 2 ? Vector2f.Zero : EvaluateDeBoor(data.SecondDerivativePoints, data.Knots.AsSpan(2, data.Knots.Length - 4), Degree - 2, amount);
    }

    public int ControlPointCount => ControlPoints.Length;
    public Vector2f GetControlPoint(int index) => ControlPoints[index];
    public ICurve2D WithControlPoints(ReadOnlySpan<Vector2f> points) => points.Length == ControlPoints.Length ? this with { ControlPoints = points } : throw new InvalidDataException("Curve point count must match its definition.");

    /// <summary>Knot insertion splits the spline into Bézier pieces exactly; the result is cached.</summary>
    public PolynomialSpans2D GetPolynomialSpans() => new(PreparePolynomialSpans(), Degree);

    /// <summary>Exact for pieces up to cubic, which covers the default degree; higher degrees are measured against chords of each piece.</summary>
    public float Distance(Vector2f point) =>
        (float)Math.Sqrt(CurveDistance2D.PolynomialDistanceSquared(point.ToVector2d(), GetPolynomialSpans(), 0, 1, double.PositiveInfinity));

    internal PositionSampler PreparePositionSampler() => new(_controlPoints!, Data.Knots, Degree);

    internal readonly struct PositionSampler(Vector2f[] points, float[] knots, int degree) : ICurve<Vector2f, float>
    {
        private readonly Vector2f[] _points = points;
        private readonly float[] _knots = knots;
        private readonly int _degree = degree;

        // Position only, with the prepared knots held directly, so sampling does not look the data up per point.
        public Interval<float> Domain => Interval.UnitInterval<float>();
        public Vector2f Evaluate(float t) => EvaluateDeBoor(_points, _knots, _degree, t);
    }

    private static float[] CreateClampedUniformKnots(int controlPointCount, int degree)
    {
        var knots = new float[controlPointCount + degree + 1];
        var spanCount = controlPointCount - degree;
        for (var index = degree + 1; index < controlPointCount; index++)
        {
            knots[index] = (index - degree) / (float)spanCount;
        }

        for (var index = controlPointCount; index < knots.Length; index++)
        {
            knots[index] = 1f;
        }

        return knots;
    }

    private static Vector2f[] CreateDerivativeControlPoints(
        ReadOnlySpan<Vector2f> controlPoints,
        ReadOnlySpan<float> knots,
        int degree)
    {
        var derivativePoints = new Vector2f[controlPoints.Length - 1];
        for (var index = 0; index < derivativePoints.Length; index++)
        {
            var denominator = knots[index + degree + 1] - knots[index + 1];
            derivativePoints[index] = degree / denominator * (controlPoints[index + 1] - controlPoints[index]);
        }
        return derivativePoints;
    }

    private static Vector2f EvaluateDeBoor(
        ReadOnlySpan<Vector2f> controlPoints,
        ReadOnlySpan<float> knots,
        int degree,
        float amount)
    {
        var span = FindKnotSpan(knots, degree, controlPoints.Length, amount);

        var values = degree <= 15
            ? stackalloc Vector2f[degree + 1]
            : new Vector2f[degree + 1];
        for (var index = 0; index <= degree; index++)
        {
            values[index] = controlPoints[span - degree + index];
        }

        for (var level = 1; level <= degree; level++)
        {
            for (var index = degree; index >= level; index--)
            {
                var knotIndex = span - degree + index;
                var lower = knots[knotIndex];
                var upper = knots[index + span - level + 1];
                var blend = upper == lower ? 0f : (amount - lower) / (upper - lower);
                values[index] = Vector2f.Lerp(values[index - 1], values[index], blend);
            }
        }
        return values[degree];
    }

    // At a repeated knot select its rightmost span, bounded by the last control point.
    private static int FindKnotSpan<TReal>(ReadOnlySpan<TReal> knots, int degree, int controlPointCount, TReal amount)
        where TReal : IComparisonOperators<TReal, TReal, bool>
    {
        var lower = degree;
        var upper = controlPointCount - 1;
        while (lower < upper)
        {
            var middle = lower + (upper - lower) / 2;
            if (amount >= knots[middle + 1]) lower = middle + 1;
            else upper = middle;
        }
        return lower;
    }
}
