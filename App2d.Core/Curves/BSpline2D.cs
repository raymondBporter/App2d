using System.Collections.ObjectModel;
using System.Numerics;

namespace App2d.Core.Curves;

/// <summary>
/// A clamped uniform B-spline. Cubic is the default; lower degrees are useful
/// for polylines and higher degrees are supported when enough control points exist.
/// </summary>
public sealed class BSpline2D : ICurve2D
{
    private readonly Vector2[] _controlPoints;
    private readonly float[] _knots;
    private readonly Vector2[] _derivativeControlPoints;
    private readonly float[] _derivativeKnots;

    public BSpline2D(IEnumerable<Vector2> controlPoints, int degree = 3)
    {
        ArgumentNullException.ThrowIfNull(controlPoints);
        if (degree < 1)
            throw new ArgumentOutOfRangeException(nameof(degree), degree, "Degree must be positive.");

        _controlPoints = [.. controlPoints];
        if (_controlPoints.Length <= degree)
        {
            throw new ArgumentException(
                $"A degree-{degree} B-spline needs at least {degree + 1} control points.",
                nameof(controlPoints));
        }

        foreach (var point in _controlPoints)
            ArgGuard.ThrowIfNotFinite(point, nameof(controlPoints));

        Degree = degree;
        ControlPoints = Array.AsReadOnly(_controlPoints);
        _knots = CreateClampedUniformKnots(_controlPoints.Length, degree);
        _derivativeControlPoints = CreateDerivativeControlPoints(_controlPoints, _knots, degree);
        _derivativeKnots = _knots[1..^1];
    }

    public int Degree { get; }
    public ReadOnlyCollection<Vector2> ControlPoints { get; }

    public Vector2 Evaluate(float amount) =>
        EvaluateDeBoor(_controlPoints, _knots, Degree, Math.Clamp(amount, 0f, 1f));

    public Vector2 EvaluateDerivative(float amount) =>
        EvaluateDeBoor(
            _derivativeControlPoints,
            _derivativeKnots,
            Degree - 1,
            Math.Clamp(amount, 0f, 1f));

    private static float[] CreateClampedUniformKnots(int controlPointCount, int degree)
    {
        var knots = new float[controlPointCount + degree + 1];
        var spanCount = controlPointCount - degree;
        for (var index = degree + 1; index < controlPointCount; index++)
            knots[index] = (index - degree) / (float)spanCount;
        for (var index = controlPointCount; index < knots.Length; index++)
            knots[index] = 1f;
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
            span++;

        Span<Vector2> values = degree <= 15
            ? stackalloc Vector2[degree + 1]
            : new Vector2[degree + 1];
        for (var index = 0; index <= degree; index++)
            values[index] = controlPoints[span - degree + index];

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
