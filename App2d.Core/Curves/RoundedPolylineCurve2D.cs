using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Curves;

/// <summary>
/// Straight spans joined by quadratic Bezier fillets. Progress follows the original polyline's XY length,
/// not the shortened curve's arc length. Each corner trims at most 48% of either adjacent span, so fillets
/// cannot overlap. Repeated points and exact reversals remain finite; an exact reversal has no unique tangent.
/// </summary>
public sealed class RoundedPolylineCurve2D : ICurve2D
{
    private readonly Vector2[] _points;
    private readonly float[] _distance, _trim;
    public ReadOnlySpan<Vector2> Points => _points;
    public float Softness { get; }
    private float Length => _distance[^1];

    public RoundedPolylineCurve2D(IEnumerable<Vector2> points, float softness = 1)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgGuard.ThrowIfNotFinite(softness);
        if (softness < 0 || softness > 1) throw new ArgumentOutOfRangeException(nameof(softness));
        _points = [.. points];
        if (_points.Length < 2) throw new ArgumentException("A rounded polyline needs at least two points.", nameof(points));
        foreach (var p in _points) ArgGuard.ThrowIfNotFinite(p, nameof(points));
        Softness = softness;
        _distance = new float[_points.Length]; _trim = new float[_points.Length];
        for (var i = 1; i < _points.Length; i++) _distance[i] = _distance[i - 1] + Vector2.Distance(_points[i - 1], _points[i]);
        for (var i = 1; i < _points.Length - 1; i++)
            _trim[i] = .48f * softness * MathF.Min(_distance[i] - _distance[i - 1], _distance[i + 1] - _distance[i]);
    }

    private (int Index, float Fraction, bool Corner) Locate(float amount)
    {
        if (Length <= 1e-8f)
        {
            var progress = Math.Clamp(amount, 0, 1) * (_points.Length - 1);
            var index = Math.Min(_points.Length - 2, (int)progress);
            return (index, progress - index, false);
        }
        var s = Math.Clamp(amount, 0, 1) * Length;
        for (var i = 1; i < _points.Length - 1; i++)
            if (_trim[i] > 1e-8f && s >= _distance[i] - _trim[i] && s <= _distance[i] + _trim[i])
                return (i, (s - _distance[i] + _trim[i]) / (2 * _trim[i]), true);
        for (var i = 0; i < _points.Length - 1; i++)
            if (_distance[i + 1] > _distance[i] && s <= _distance[i + 1])
                return (i, (s - _distance[i]) / (_distance[i + 1] - _distance[i]), false);
        return (_points.Length - 2, 1, false);
    }

    private QuadraticBezier2D Corner(int i) => new(
        Vector2.Lerp(_points[i], _points[i - 1], _trim[i] / (_distance[i] - _distance[i - 1])),
        _points[i],
        Vector2.Lerp(_points[i], _points[i + 1], _trim[i] / (_distance[i + 1] - _distance[i])));

    public Vector2 Evaluate(float amount)
    {
        if (amount <= 0) return _points[0];
        if (amount >= 1) return _points[^1];
        var (i, t, corner) = Locate(amount);
        return corner ? Corner(i).Evaluate(t) : Vector2.Lerp(_points[i], _points[i + 1], t);
    }

    public Vector2 EvaluateDerivative(float amount)
    {
        var (i, t, corner) = Locate(amount);
        if (corner) return Corner(i).EvaluateDerivative(t) * (Length / (2 * _trim[i]));
        var span = _distance[i + 1] - _distance[i];
        return span > 1e-8f ? (_points[i + 1] - _points[i]) * (Length / span) : Vector2.Zero;
    }

    /// <summary>Interpolates a scalar per control point (e.g. depth or thickness) with the same fillet weights as XY.</summary>
    public float Interpolate(ReadOnlySpan<float> values, float amount)
    {
        if (values.Length != _points.Length) throw new ArgumentException("Provide one value per control point.", nameof(values));
        if (amount <= 0) return values[0];
        if (amount >= 1) return values[^1];
        var (i, t, corner) = Locate(amount);
        if (!corner) return values[i] + (values[i + 1] - values[i]) * t;
        var before = (1 - t) * (1 - t) * _trim[i] / (_distance[i] - _distance[i - 1]);
        var after = t * t * _trim[i] / (_distance[i + 1] - _distance[i]);
        return before * values[i - 1] + (1 - before - after) * values[i] + after * values[i + 1];
    }

    /// <summary>Uniform base samples plus exact span/fillet boundaries and corner midpoints.</summary>
    public float[] SampleParameters(int sections)
    {
        ArgGuard.ThrowIfNotPositive(sections);
        var samples = new SortedSet<float>(Enumerable.Range(0, sections + 1).Select(i => i / (float)sections));
        if (Length > 1e-8f)
            for (var i = 1; i < _points.Length - 1; i++)
            {
                samples.Add((_distance[i] - _trim[i]) / Length);
                samples.Add(_distance[i] / Length);
                samples.Add((_distance[i] + _trim[i]) / Length);
            }
        return [.. samples];
    }

    public float Distance(Vector2 point) => CurveDistance2D.SampledDistance(point, this, 128);
}
