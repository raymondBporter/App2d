using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Curves;

/// <summary>An open polyline parameterized uniformly across its segments.</summary>
public sealed class PolylineCurve2D : ICurve2D
{
    private readonly Vector2[] _points;

    public PolylineCurve2D(IEnumerable<Vector2> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        _points = [.. points];
        if (_points.Length < 2) throw new ArgumentException("A polyline needs at least two points.", nameof(points));
        foreach (var point in _points) ArgGuard.ThrowIfNotFinite(point, nameof(points));
    }

    public ReadOnlySpan<Vector2> Points => _points;

    public Vector2 Evaluate(float amount)
    {
        var (index, fraction) = Segment(amount);
        return Vector2.Lerp(_points[index], _points[index + 1], fraction);
    }

    public Vector2 EvaluateDerivative(float amount)
    {
        var (index, _) = Segment(amount);
        return (_points[index + 1] - _points[index]) * (_points.Length - 1);
    }

    private (int Index, float Fraction) Segment(float amount)
    {
        var progress = Math.Clamp(amount, 0f, 1f) * (_points.Length - 1);
        var index = Math.Min(_points.Length - 2, (int)progress);
        return (index, progress - index);
    }
}
