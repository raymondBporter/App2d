using App2d.Core.Geometry.Distance;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Curves;

/// <summary>
/// Straight spans joined by quadratic Bezier fillets. Progress follows the original polyline's XY length,
/// not the shortened curve's arc length. Each corner trims at most 48% of either adjacent span, so fillets
/// cannot overlap. Repeated points and exact reversals remain finite; an exact reversal has no unique tangent.
/// </summary>
public readonly partial record struct RoundedPolylineCurve2D : ICurve2D, ITwiceDifferentiableCurve<Vector2f, float>, IControlPointCurve<Vector2f, ICurve2D>, IPolynomialCurve2D
{
    public GeometryKind Kind => GeometryKind.RoundedPolyline;
    public Interval<float> Domain => Interval.UnitInterval<float>();

    private readonly Vector2f[]? _points;
    private readonly float[]? _distance, _trim;
    private readonly float _softness = 1;

    public RoundedPolylineCurve2D(IEnumerable<Vector2f> points, float softness = 1)
    {
        _points = points?.ToArray();
        _softness = softness;
        (_distance, _trim) = Measure(_points, softness);
    }

    public ReadOnlySpan<Vector2f> Points
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

    private static (float[] Distance, float[] Trim) Measure(Vector2f[]? points, float softness)
    {
        if (points is null || points.Length < 2)
        {
            return ([], []);
        }

        var distance = new float[points.Length];
        var trim = new float[points.Length];
        for (var i = 1; i < points.Length; i++)
        {
            distance[i] = distance[i - 1] + Vector2f.Distance(points[i - 1], points[i]);
        }

        var scale = .48f * Realf.Clamp01(float.IsFinite(softness) ? softness : 0);
        for (var i = 1; i < points.Length - 1; i++)
        {
            trim[i] = scale * MathF.Min(distance[i] - distance[i - 1], distance[i + 1] - distance[i]);
        }

        return (distance, trim);
    }

    private enum SpanKind { Point, Line, Corner }
    private readonly record struct SampleLocation(int Index, float Fraction, SpanKind Kind, float Before = 0, float After = 0);

    private SampleLocation Locate(float amount)
    {
        var points = Points;
        var distance = _distance!;
        var trim = _trim!;
        if (Length <= 1e-8f)
        {
            var progress = Realf.Clamp01(amount) * (points.Length - 1);
            var index = Math.Min(points.Length - 2, (int)progress);
            return new(index, progress - index, SpanKind.Line);
        }
        var s = Realf.Clamp01(amount) * Length;
        for (var i = 1; i < points.Length - 1; i++)
        {
            if (trim[i] > 1e-8f && s >= distance[i] - trim[i] && s <= distance[i] + trim[i])
            {
                var (before, after) = CornerFractions(i);
                return new(i, (s - distance[i] + trim[i]) / (2 * trim[i]), SpanKind.Corner, before, after);
            }
        }

        for (var i = 0; i < points.Length - 1; i++)
        {
            if (distance[i + 1] > distance[i] && s <= distance[i + 1])
            {
                return new(i, (s - distance[i]) / (distance[i + 1] - distance[i]), SpanKind.Line);
            }
        }

        return new(points.Length - 2, 1, SpanKind.Line);
    }

    private (float Before, float After) CornerFractions(int index)
    {
        var distance = _distance!;
        var trim = _trim![index];
        return (trim / (distance[index] - distance[index - 1]), trim / (distance[index + 1] - distance[index]));
    }

    private QuadraticBezier2D Corner(int index)
    {
        var (before, after) = CornerFractions(index);
        return Corner(index, before, after);
    }

    private QuadraticBezier2D Corner(int index, float before, float after)
    {
        var points = Points;
        var control = points[index];
        return QuadraticBezier2D.Builder.Create(
            Vector2f.Lerp(control, points[index - 1], before), control, Vector2f.Lerp(control, points[index + 1], after));
    }

    private SampleLocation LocateSample(float amount)
    {
        if (amount <= 0) return new(0, 0, SpanKind.Point);
        if (amount >= 1) return new(Points.Length - 1, 0, SpanKind.Point);
        return Locate(amount);
    }

    public Vector2f Evaluate(float amount) => Evaluate(LocateSample(amount));

    private Vector2f Evaluate(SampleLocation sample)
    {
        var points = Points;
        return sample.Kind switch
        {
            SpanKind.Point => points[sample.Index],
            SpanKind.Line => Vector2f.Lerp(points[sample.Index], points[sample.Index + 1], sample.Fraction),
            _ => Corner(sample.Index, sample.Before, sample.After).Evaluate(sample.Fraction)
        };
    }

    public Vector2f Derivative(float amount)
    {
        var points = Points;
        var sample = Locate(amount);
        var i = sample.Index;
        if (sample.Kind == SpanKind.Corner)
        {
            return Corner(i, sample.Before, sample.After).Derivative(sample.Fraction) * (Length / (2 * _trim![i]));
        }

        var span = _distance![i + 1] - _distance[i];
        return span > 1e-8f ? (points[i + 1] - points[i]) * (Length / span) : Vector2f.Zero;
    }

    public int ControlPointCount => Points.Length;
    public Vector2f GetControlPoint(int index) => Points[index];
    public ICurve2D WithControlPoints(ReadOnlySpan<Vector2f> points) => points.Length == Points.Length ? this with { Points = points } : throw new InvalidDataException("Curve point count must match its definition.");

    /// <summary>Zero on the straight parts; in a corner, the fillet's constant second derivative scaled to this parameter.</summary>
    public Vector2f SecondDerivative(float amount)
    {
        var sample = Locate(amount);
        if (sample.Kind != SpanKind.Corner) return Vector2f.Zero;
        var scale = Length / (2 * _trim![sample.Index]);
        return Corner(sample.Index, sample.Before, sample.After).SecondDerivative(sample.Fraction) * (scale * scale);
    }

    /// <summary>Interpolates caller-supplied scalar values (e.g. depth) with the same fillet weights as XY.</summary>
    public float Interpolate(ReadOnlySpan<float> values, float amount)
    {
        ValidateValueCount(values);
        return Interpolate(values, LocateSample(amount));
    }

    internal (Vector2f Position, float Value) EvaluateWithAttribute(ReadOnlySpan<float> values, float amount)
    {
        ValidateValueCount(values);
        var sample = LocateSample(amount);
        return (Evaluate(sample), Interpolate(values, sample));
    }

    private static float Interpolate(ReadOnlySpan<float> values, SampleLocation sample)
    {
        var index = sample.Index;
        var value = values[index];
        if (sample.Kind == SpanKind.Point) return value;
        if (sample.Kind == SpanKind.Line) return value + (values[index + 1] - value) * sample.Fraction;

        // The same quadratic as XY, with wider intermediates for large finite scalar attributes.
        var start = Interval.Lerp<double>(value, values[index - 1], sample.Before);
        var end = Interval.Lerp<double>(value, values[index + 1], sample.After);
        return (float)BezierMath.Quadratic(start, (double)value, end, (double)sample.Fraction);
    }

    private void ValidateValueCount(ReadOnlySpan<float> values)
    {
        if (values.Length != Points.Length)
            throw new ArgumentException("Provide one value per control point.", nameof(values));
    }

    /// <summary>Uniform base samples plus exact span/fillet boundaries and corner midpoints.</summary>
    public float[] SampleParameters(int sections)
    {
        Ensure.Positive(sections);
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

    /// <summary>Straight runs and quadratic fillets, cached with the measured lengths.</summary>
    public PolynomialSpans2D GetPolynomialSpans() => new(PreparePolynomialSpans(), 2);

    public float Distance(Vector2f point) => Distance(point, 0, 1);

    internal float Distance(Vector2f point, double from, double to)
    {
        // Evaluate can still return a distinct endpoint if accumulated float lengths merge adjacent spans.
        var target = point.ToVector2d();
        var first = (Evaluate((float)from).ToVector2d() - target).LengthSquared();
        var last = (Evaluate((float)to).ToVector2d() - target).LengthSquared();
        return (float)Math.Sqrt(CurveDistance2D.PolynomialDistanceSquared(target, GetPolynomialSpans(), from, to, Math.Min(first, last)));
    }
}
