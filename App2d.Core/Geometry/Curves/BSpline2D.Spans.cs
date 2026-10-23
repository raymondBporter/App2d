using App2d.Core.Mathematics;

namespace App2d.Core.Geometry.Curves;

public readonly partial record struct BSpline2D
{
    /// <summary>Knot insertion preserves the curve; this is not sampling. Cached with the existing prepared data.</summary>
    internal PolynomialCurveSpan2D[] GetPolynomialSpans()
    {
        var prepared = Data;
        lock (prepared)
        {
            if (prepared.RaycastSpans is { } cached) return cached;
            var points = _controlPoints!.Select(p => p.ToVector2d()).ToArray();
            var knots = prepared.Knots.Select(k => (double)k).ToArray();
            var degree = Degree;
            foreach (var knot in prepared.Knots.Distinct().Where(k => k > 0 && k < 1))
            {
                var multiplicity = knots.Count(k => k == knot);
                for (; multiplicity < degree; multiplicity++) Insert(knot, multiplicity);
            }
            var spans = new List<PolynomialCurveSpan2D>();
            for (var i = degree; i < points.Length; i++)
                if (knots[i] < knots[i + 1]) spans.Add(new(knots[i], knots[i + 1], points[(i - degree)..(i + 1)]));
            return prepared.RaycastSpans = [.. spans];

            void Insert(double knot, int multiplicity)
            {
                var span = degree;
                while (span < points.Length - 1 && knot >= knots[span + 1]) span++;
                var nextPoints = new Vector2d[points.Length + 1];
                var nextKnots = new double[knots.Length + 1];
                Array.Copy(knots, 0, nextKnots, 0, span + 1);
                nextKnots[span + 1] = knot;
                Array.Copy(knots, span + 1, nextKnots, span + 2, knots.Length - span - 1);
                Array.Copy(points, 0, nextPoints, 0, span - degree + 1);
                Array.Copy(points, span - multiplicity, nextPoints, span - multiplicity + 1, points.Length - span + multiplicity);
                for (var i = span - degree + 1; i <= span - multiplicity; i++)
                {
                    var blend = (knot - knots[i]) / (knots[i + degree] - knots[i]);
                    nextPoints[i] = points[i - 1] * (1 - blend) + points[i] * blend;
                }
                points = nextPoints;
                knots = nextKnots;
            }
        }
    }
}
