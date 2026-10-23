using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry.Curves;

/// <summary>
/// Straight spans joined by quadratic Bezier fillets. Progress follows the original polyline's XY length,
/// not the shortened curve's arc length. Each corner trims at most 48% of either adjacent span, so fillets
/// cannot overlap. Repeated points and exact reversals remain finite; an exact reversal has no unique tangent.
/// </summary>
public readonly partial record struct RoundedPolylineCurve2D : ICurve2D
{
    public string Kind => CurveKinds2D.RoundedPolyline;

    private readonly Vector2[]? _points;
    private readonly float[]? _distance, _trim;
    private readonly float _softness = 1;

    public RoundedPolylineCurve2D(IEnumerable<Vector2> points, float softness = 1)
    {
        _points = points?.ToArray();
        _softness = softness;
        (_distance, _trim) = Measure(_points, softness);
    }

    public ReadOnlySpan<Vector2> Points
    {
        get => _points;
        init
        {
            _points = value.ToArray();
            (_distance, _trim) = Measure(_points, _softness);
        }
    }

    public float Softness
    {
        get => _softness;
        init
        {
            _softness = value;
            (_distance, _trim) = Measure(_points, value);
        }
    }

    private float Length => _distance is { Length: > 0 } ? _distance[^1] : 0;

    private static (float[] Distance, float[] Trim) Measure(Vector2[]? points, float softness)
    {
        if (points is null || points.Length < 2)
        {
            return ([], []);
        }

        var distance = new float[points.Length];
        var trim = new float[points.Length];
        for (var i = 1; i < points.Length; i++)
        {
            distance[i] = distance[i - 1] + Vector2.Distance(points[i - 1], points[i]);
        }

        var scale = .48f * Clamping.Clamp01(float.IsFinite(softness) ? softness : 0);
        for (var i = 1; i < points.Length - 1; i++)
        {
            trim[i] = scale * MathF.Min(distance[i] - distance[i - 1], distance[i + 1] - distance[i]);
        }

        return (distance, trim);
    }

    private (int Index, float Fraction, bool Corner) Locate(float amount)
    {
        var points = Points;
        var distance = _distance!;
        var trim = _trim!;
        if (Length <= 1e-8f)
        {
            var progress = Clamping.Clamp01(amount) * (points.Length - 1);
            var index = Math.Min(points.Length - 2, (int)progress);
            return (index, progress - index, false);
        }
        var s = Clamping.Clamp01(amount) * Length;
        for (var i = 1; i < points.Length - 1; i++)
        {
            if (trim[i] > 1e-8f && s >= distance[i] - trim[i] && s <= distance[i] + trim[i])
            {
                return (i, (s - distance[i] + trim[i]) / (2 * trim[i]), true);
            }
        }

        for (var i = 0; i < points.Length - 1; i++)
        {
            if (distance[i + 1] > distance[i] && s <= distance[i + 1])
            {
                return (i, (s - distance[i]) / (distance[i + 1] - distance[i]), false);
            }
        }

        return (points.Length - 2, 1, false);
    }

    private QuadraticBezier2D Corner(int i)
    {
        var points = Points;
        var distance = _distance!;
        var trim = _trim!;
        return QuadraticBezier2D.Builder.Create(
            Vector2.Lerp(points[i], points[i - 1], trim[i] / (distance[i] - distance[i - 1])),
            points[i],
            Vector2.Lerp(points[i], points[i + 1], trim[i] / (distance[i + 1] - distance[i])));
    }

    public Vector2 Evaluate(float amount)
    {
        var points = Points;
        if (amount <= 0)
        {
            return points[0];
        }

        if (amount >= 1)
        {
            return points[^1];
        }

        var (i, t, corner) = Locate(amount);
        return corner ? Corner(i).Evaluate(t) : Vector2.Lerp(points[i], points[i + 1], t);
    }

    public Vector2 EvaluateDerivative(float amount)
    {
        var points = Points;
        var (i, t, corner) = Locate(amount);
        if (corner)
        {
            return Corner(i).EvaluateDerivative(t) * (Length / (2 * _trim![i]));
        }

        var span = _distance![i + 1] - _distance[i];
        return span > 1e-8f ? (points[i + 1] - points[i]) * (Length / span) : Vector2.Zero;
    }

    /// <summary>Interpolates a scalar per control point (e.g. depth or thickness) with the same fillet weights as XY.</summary>
    public float Interpolate(ReadOnlySpan<float> values, float amount)
    {
        if (values.Length != Points.Length)
        {
            throw new ArgumentException("Provide one value per control point.", nameof(values));
        }

        if (amount <= 0)
        {
            return values[0];
        }

        if (amount >= 1)
        {
            return values[^1];
        }

        var (i, t, corner) = Locate(amount);
        if (!corner)
        {
            return values[i] + (values[i + 1] - values[i]) * t;
        }

        var distance = _distance!;
        var trim = _trim!;
        var before = (1 - t) * (1 - t) * trim[i] / (distance[i] - distance[i - 1]);
        var after = t * t * trim[i] / (distance[i + 1] - distance[i]);
        return before * values[i - 1] + (1 - before - after) * values[i] + after * values[i + 1];
    }

    /// <summary>Uniform base samples plus exact span/fillet boundaries and corner midpoints.</summary>
    public float[] SampleParameters(int sections)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sections);
        var samples = new SortedSet<float>(Enumerable.Range(0, sections + 1).Select(i => i / (float)sections));
        if (Length > 1e-8f)
        {
            var distance = _distance!;
            var trim = _trim!;
            for (var i = 1; i < Points.Length - 1; i++)
            {
                samples.Add((distance[i] - trim[i]) / Length);
                samples.Add(distance[i] / Length);
                samples.Add((distance[i] + trim[i]) / Length);
            }
        }
        return [.. samples];
    }

    public float Distance(Vector2 point) => CurveDistance2D.SampledDistance(point, this, 128);
}
