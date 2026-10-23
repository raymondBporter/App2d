using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry.Curves;

/// <summary>An open polyline parameterized uniformly across its segments.</summary>
public readonly partial record struct PolylineCurve2D : ICurve2D
{
    public string Kind => CurveKinds2D.Polyline;

    private readonly Vector2[]? _points;

    public PolylineCurve2D(IEnumerable<Vector2> points)
    {
        _points = points?.ToArray();
    }

    public ReadOnlySpan<Vector2> Points { get => _points; init => _points = value.ToArray(); }

    public Vector2 Evaluate(float amount)
    {
        var (index, fraction) = Segment(amount);
        return Vector2.Lerp(Points[index], Points[index + 1], fraction);
    }

    public Vector2 EvaluateDerivative(float amount)
    {
        var (index, _) = Segment(amount);
        return (Points[index + 1] - Points[index]) * (Points.Length - 1);
    }

    public float Distance(Vector2 point)
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
        var progress = Clamping.Clamp01(amount) * (Points.Length - 1);
        var index = Math.Min(Points.Length - 2, (int)progress);
        return (index, progress - index);
    }
}
