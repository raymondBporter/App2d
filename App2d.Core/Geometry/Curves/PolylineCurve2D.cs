using App2d.Core.Geometry.Distance;

namespace App2d.Core.Geometry.Curves;

/// <summary>An open polyline parameterized uniformly across its segments.</summary>
public readonly partial record struct PolylineCurve2D : ICurve2D, ITwiceDifferentiableCurve<Vector2f, float>, IControlPointCurve<Vector2f, ICurve2D>, IPolynomialCurve2D
{
    public GeometryKind Kind => GeometryKind.Polyline;
    public Interval<float> Domain => Interval.UnitInterval<float>();

    private readonly Vector2f[]? _points;

    public PolylineCurve2D(IEnumerable<Vector2f> points)
    {
        _points = points?.ToArray();
    }

    public ReadOnlySpan<Vector2f> Points { get => _points; init => _points = value.ToArray(); }

    public Vector2f Evaluate(float amount)
    {
        var (index, fraction) = Segment(amount);
        return Vector2f.Lerp(Points[index], Points[index + 1], fraction);
    }

    public Vector2f Derivative(float amount)
    {
        if (float.IsNaN(amount)) return new Vector2f(float.NaN, float.NaN);
        var (index, _) = Segment(amount);
        return (Points[index + 1] - Points[index]) * (Points.Length - 1);
    }

    /// <summary>Zero on every segment. At a corner this is the value for the segment that starts there.</summary>
    public Vector2f SecondDerivative(float amount) => Vector2f.Zero;

    public int ControlPointCount => Points.Length;
    public Vector2f GetControlPoint(int index) => Points[index];
    public ICurve2D WithControlPoints(ReadOnlySpan<Vector2f> points) => points.Length == Points.Length ? this with { Points = points } : throw new InvalidDataException("Curve point count must match its definition.");

    public PolynomialSpans2D GetPolynomialSpans() => new(_points!);

    public float Distance(Vector2f point)
    {
        var nearestSquared = float.PositiveInfinity;
        for (var i = 1; i < Points.Length; i++)
        {
            nearestSquared = MathF.Min(nearestSquared, Distance2D.DistanceSquaredToSegment(point, Points[i - 1], Points[i]));
        }

        return MathF.Sqrt(nearestSquared);
    }

    private (int Index, float Fraction) Segment(float amount)
    {
        // Outside [0, 1] the first and last segments continue as straight lines.
        var progress = amount * (Points.Length - 1);
        var index = Math.Clamp((int)progress, 0, Points.Length - 2);
        return (index, progress - index);
    }
}
