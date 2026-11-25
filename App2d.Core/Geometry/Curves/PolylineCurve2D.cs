using App2d.Core.Geometry.Distance;

namespace App2d.Core.Geometry.Curves;

/// <summary>An open polyline parameterized uniformly across its segments.</summary>
public readonly partial record struct PolylineCurve2D : ICurve2D
{
    public GeometryKind Kind => GeometryKind.Polyline;

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
