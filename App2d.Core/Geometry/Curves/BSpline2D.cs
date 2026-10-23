using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Mathematics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Geometry.Curves;

/// <summary>
/// A clamped uniform B-spline. Cubic is the default; lower degrees are useful
/// for polylines and higher degrees are supported when enough control points exist.
/// </summary>
public readonly partial record struct BSpline2D : ICurve2D
{
    public string Kind => CurveKinds2D.BSpline;

    private readonly Vector2[]? _controlPoints;
    private static readonly ConditionalWeakTable<Vector2[], Dictionary<int, Prepared>> PreparedCurves = [];

    public BSpline2D(IEnumerable<Vector2> controlPoints, int degree = 3)
    {
        _controlPoints = controlPoints?.ToArray();
        Degree = degree;
    }

    public int Degree { get; init; } = 3;
    public BSpline2D() { }
    public ReadOnlySpan<Vector2> ControlPoints { get => _controlPoints; init => _controlPoints = value.ToArray(); }

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
                    data = new(knots, CreateDerivativeControlPoints(_controlPoints, knots, Degree), knots[1..^1]);
                    entries[Degree] = data;
                }
                return data;
            }
        }
    }
    private sealed record Prepared(float[] Knots, Vector2[] DerivativePoints, float[] DerivativeKnots)
    {
        internal PolynomialCurveSpan2D[]? RaycastSpans;
    }

    public Vector2 Evaluate(float amount) => EvaluateDeBoor(_controlPoints, Data.Knots, Degree, Clamping.Clamp01(amount));
    public Vector2 EvaluateDerivative(float amount)
    {
        var data = Data;
        return EvaluateDeBoor(data.DerivativePoints, data.DerivativeKnots, Degree - 1, Clamping.Clamp01(amount));
    }
    public float Distance(Vector2 point) => CurveDistance2D.SampledDistance(point, this,
        Clamping.Clamp((ControlPoints.Length - Degree) * 32, 64, 1024));

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

    private static Vector2[] CreateDerivativeControlPoints(
        ReadOnlySpan<Vector2> controlPoints,
        ReadOnlySpan<float> knots,
        int degree)
    {
        var derivativePoints = new Vector2[controlPoints.Length - 1];
        for (var index = 0; index < derivativePoints.Length; index++)
        {
            var denominator = knots[index + degree + 1] - knots[index + 1];
            derivativePoints[index] = degree / denominator * (controlPoints[index + 1] - controlPoints[index]);
        }
        return derivativePoints;
    }

    private static Vector2 EvaluateDeBoor(
        ReadOnlySpan<Vector2> controlPoints,
        ReadOnlySpan<float> knots,
        int degree,
        float amount)
    {
        var span = degree;
        while (span < controlPoints.Length - 1 && amount >= knots[span + 1])
        {
            span++;
        }

        var values = degree <= 15
            ? stackalloc Vector2[degree + 1]
            : new Vector2[degree + 1];
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
                values[index] = Vector2.Lerp(values[index - 1], values[index], blend);
            }
        }
        return values[degree];
    }
}
