using App2d.Core.Mathematics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Geometry.Curves;

public readonly partial record struct RoundedPolylineCurve2D
{
    private static readonly ConditionalWeakTable<float[], PolynomialCurveSpan2D[]> PolynomialSpans = [];

    internal PolynomialCurveSpan2D[] GetPolynomialSpans()
    {
        // The measured array is immutable and unique to this points/softness pair.
        return PolynomialSpans.TryGetValue(_distance!, out var spans) ? spans : Prepare(this);
    }

    private static PolynomialCurveSpan2D[] Prepare(RoundedPolylineCurve2D curve) =>
        PolynomialSpans.GetValue(curve._distance!, _ => curve.CreatePolynomialSpans());

    private PolynomialCurveSpan2D[] CreatePolynomialSpans()
    {
        var spans = new List<PolynomialCurveSpan2D>();
        if (Length <= 1e-8f)
        {
            for (var i = 0; i < Points.Length - 1; i++)
                spans.Add(new(i / (double)(Points.Length - 1), (i + 1d) / (Points.Length - 1),
                    [Points[i].ToVector2d(), Points[i + 1].ToVector2d()]));
            return [.. spans];
        }
        for (var i = 0; i < Points.Length - 1; i++)
        {
            var distance = _distance![i + 1] - _distance[i];
            var startTrim = _trim![i] > 1e-8f ? _trim[i] : 0;
            var endTrim = _trim[i + 1] > 1e-8f ? _trim[i + 1] : 0;
            if (distance > 0 && distance > startTrim + endTrim)
            {
                var start = Points[i].ToVector2d();
                var edge = Points[i + 1].ToVector2d() - start;
                spans.Add(new(((double)_distance[i] + startTrim) / Length, ((double)_distance[i + 1] - endTrim) / Length,
                    [start + edge * (startTrim / distance), start + edge * (1d - endTrim / distance)]));
            }
            if (endTrim > 0)
            {
                var corner = Corner(i + 1);
                spans.Add(new(((double)_distance[i + 1] - endTrim) / Length, ((double)_distance[i + 1] + endTrim) / Length,
                    [corner.Start.ToVector2d(), corner.Control.ToVector2d(), corner.End.ToVector2d()]));
            }
        }
        return [.. spans];
    }
}
