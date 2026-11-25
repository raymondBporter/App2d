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
            var controls = ControlPoints;
            var sourceKnots = prepared.Knots;
            var points = new Vector2d[controls.Length];
            var knots = new double[sourceKnots.Length];
            for (var i = 0; i < controls.Length; i++) points[i] = controls[i].ToVector2d();
            for (var i = 0; i < sourceKnots.Length; i++) knots[i] = sourceKnots[i];
            var degree = Degree;
            for (var i = degree + 1; i < controls.Length;)
            {
                var start = i;
                var knot = sourceKnots[i++];
                while (i < controls.Length && sourceKnots[i] == knot) i++;
                if (knot <= 0 || knot >= 1) continue;
                for (var multiplicity = i - start; multiplicity < degree; multiplicity++)
                    InsertKnot(ref points, ref knots, degree, knot, multiplicity);
            }
            var spans = new List<PolynomialCurveSpan2D>();
            for (var i = degree; i < points.Length; i++)
                if (knots[i] < knots[i + 1]) spans.Add(new(knots[i], knots[i + 1], points[(i - degree)..(i + 1)]));
            return prepared.RaycastSpans = spans.ToArray();
        }
    }

    private static void InsertKnot(ref Vector2d[] points, ref double[] knots, int degree, double knot, int multiplicity)
    {
        var span = FindKnotSpan(knots, degree, points.Length, knot);
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
            nextPoints[i] = BezierMath.Blend(points[i - 1], points[i], 1 - blend, blend);
        }
        points = nextPoints;
        knots = nextKnots;
    }
}
