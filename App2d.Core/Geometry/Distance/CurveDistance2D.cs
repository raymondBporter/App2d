using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Curves;
using App2d.Core.Mathematics.RootFinding;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Distance;

/// <summary>Point distance queries for finite curves, with polyline sampling for curves without a direct query.</summary>
public static class CurveDistance2D
{
    public static float Distance(Vector2f point, ICurve2D curve)
    {
        Ensure.ThrowIfNull(curve);
        return curve.Distance(point);
    }

    public static float Distance(ICurve2D curve, Vector2f point) => Distance(point, curve);

    /// <summary>Distance to the part of a curve between two ordered parameters in [0, 1]. Equal parameters measure to that one point.</summary>
    public static float Distance(Vector2f point, ICurve2D curve, float from, float to)
    {
        Ensure.ThrowIfNull(curve);
        Ensure.InRange(from, 0f, 1f);
        Ensure.InRange(to, from, 1f);
        return DistanceOnInterval(point, curve, from, to);
    }

    internal static float DistanceOnInterval(Vector2f point, ICurve2D curve, double from, double to)
    {
        if (from == to) return (float)(curve.Evaluate((float)from).ToVector2d() - point.ToVector2d()).Length();
        if (from == 0 && to == 1) return curve.Distance(point);

        // An arc has its own closed form. Everything made of polynomial pieces shares one solver; the rest is sampled.
        return curve switch
        {
            Arc2D arc => arc.Distance(point, from, to),
            RoundedPolylineCurve2D rounded => rounded.Distance(point, from, to),
            IPolynomialCurve2D polynomial => (float)Math.Sqrt(PolynomialDistanceSquared(point.ToVector2d(), polynomial.GetPolynomialSpans(), from, to, double.PositiveInfinity)),
            _ => SampledDistance(point, curve, 128, from, to)
        };
    }

    /// <summary>
    /// The squared distance to the pieces inside [from, to], or the given starting value if none is nearer. A piece
    /// whose control hull is already farther than the best so far is skipped without solving.
    /// </summary>
    internal static double PolynomialDistanceSquared(Vector2d point, PolynomialSpans2D spans, double from, double to, double nearestSquared)
    {
        Span<Vector2d> storage = spans.MaximumDegree < 32 ? stackalloc Vector2d[spans.MaximumDegree + 1] : new Vector2d[spans.MaximumDegree + 1];
        for (var i = 0; i < spans.Count; i++)
        {
            var range = spans.Get(i, storage);
            if (range.From > to) break;
            var low = Math.Max(from, range.From);
            var high = Math.Min(to, range.To);
            if (low > high) continue;
            ReadOnlySpan<Vector2d> controls = storage[..range.ControlCount];
            if (ControlBoundsDistanceSquared(point, controls) >= nearestSquared) continue;

            var width = range.To - range.From;
            var localFrom = (low - range.From) / width;
            var localTo = (high - range.From) / width;
            var distanceSquared = controls.Length switch
            {
                2 => SegmentDistanceSquared(point, controls[0], controls[1], localFrom, localTo),
                3 => QuadraticDistanceSquared(point, controls[0], controls[1], controls[2], localFrom, localTo),
                4 => CubicDistanceSquared(point, controls[0], controls[1], controls[2], controls[3], localFrom, localTo),
                _ => SampledSpanDistanceSquared(point, controls, localFrom, localTo)
            };
            nearestSquared = Math.Min(nearestSquared, distanceSquared);
            if (nearestSquared == 0) break;
        }
        return nearestSquared;
    }

    /// <summary>
    /// The exact squared distance to a cubic Bézier over [from, to]. The nearest point is an end of the interval or
    /// a parameter where the vector to the point is perpendicular to the tangent: a root of a degree-5 polynomial.
    /// </summary>
    internal static double CubicDistanceSquared(Vector2d point, Vector2d start, Vector2d control1, Vector2d control2, Vector2d end, double from, double to)
    {
        // B(t) - point in the power basis: c0 + c1 t + c2 t^2 + c3 t^3.
        var firstEdge = control1 - start;
        var secondEdge = control2 - control1;
        var thirdEdge = end - control2;
        var c0 = start - point;
        var c1 = firstEdge * 3;
        var c2 = (secondEdge - firstEdge) * 3;
        var c3 = thirdEdge - secondEdge * 2 + firstEdge;

        // The curve's own ends are taken from the controls, not summed back from the coefficients.
        var first = from == 0 ? c0 : Cubic(from);
        var last = to == 1 ? end - point : Cubic(to);
        var nearestSquared = Math.Min(first.LengthSquared(), last.LengthSquared());
        if (nearestSquared == 0 || from == to) return nearestSquared;

        // (B - point) . B' in ascending powers of t.
        Span<double> stationary =
        [
            c0.Dot(c1),
            c1.Dot(c1) + 2 * c0.Dot(c2),
            3 * c0.Dot(c3) + 3 * c1.Dot(c2),
            4 * c1.Dot(c3) + 2 * c2.Dot(c2),
            5 * c2.Dot(c3),
            3 * c3.Dot(c3)
        ];
        var magnitude = 0d;
        foreach (var coefficient in stationary) magnitude += Math.Abs(coefficient);
        // A NaN or infinite control leaves nothing to solve and the chords report it; a curve that never moves has no stationary equation.
        if (!double.IsFinite(magnitude)) return Math.Min(nearestSquared, SampledSpanDistanceSquared(point, [start, control1, control2, end], from, to));
        if (magnitude == 0) return nearestSquared;

        Span<double> roots = stackalloc double[5];
        var count = PolynomialRoots.FindRealRoots(stationary, from, to, roots);
        for (var i = 0; i < count; i++) nearestSquared = Math.Min(nearestSquared, Cubic(roots[i]).LengthSquared());
        return nearestSquared;

        Vector2d Cubic(double t) => ((c3 * t + c2) * t + c1) * t + c0;
    }

    // A piece with no closed-form solver here is measured against chords of its own Bézier.
    private static double SampledSpanDistanceSquared(Vector2d point, ReadOnlySpan<Vector2d> controls, double from, double to)
    {
        const int Chords = 64;
        var previous = BezierMath.EvaluateControls(controls, from);
        var nearestSquared = double.PositiveInfinity;
        for (var i = 1; i <= Chords; i++)
        {
            var next = BezierMath.EvaluateControls(controls, Interval.Lerp(from, to, i / (double)Chords));
            nearestSquared = Math.Min(nearestSquared, SegmentDistanceSquared(point, previous, next, 0, 1));
            previous = next;
        }
        return nearestSquared;
    }

    private static double ControlBoundsDistanceSquared(Vector2d point, ReadOnlySpan<Vector2d> controls)
    {
        var minX = controls[0].X;
        var minY = controls[0].Y;
        var maxX = minX;
        var maxY = minY;
        foreach (var control in controls[1..])
        {
            minX = Math.Min(minX, control.X);
            minY = Math.Min(minY, control.Y);
            maxX = Math.Max(maxX, control.X);
            maxY = Math.Max(maxY, control.Y);
        }
        var x = Math.Max(0, Math.Max(minX - point.X, point.X - maxX));
        var y = Math.Max(0, Math.Max(minY - point.Y, point.Y - maxY));
        return x * x + y * y;
    }

    internal static double QuadraticDistanceSquared(Vector2d point, Vector2d start, Vector2d control, Vector2d end,
        double from, double to)
    {
        var firstEdge = control - start;
        var lastEdge = end - control;
        var quadratic = lastEdge - firstEdge;
        if (quadratic == Vector2d.Zero) return SegmentDistanceSquared(point, start, end, from, to);

        var relativeStart = start - point;
        var relativeControl = control - point;
        var relativeEnd = end - point;
        var first = from == 0 ? relativeStart : BezierMath.Quadratic(relativeStart, relativeControl, relativeEnd, from);
        var last = to == 1 ? relativeEnd : BezierMath.Quadratic(relativeStart, relativeControl, relativeEnd, to);
        var nearestSquared = Math.Min(first.LengthSquared(), last.LengthSquared());
        if (nearestSquared == 0 || from == to || !double.IsFinite(nearestSquared)) return nearestSquared;

        // The Bernstein coefficients of (B - point) · B' give a cheap monotonicity check.
        // Each coefficient can be scaled by a positive constant because only its sign matters here.
        var startDot = relativeStart.Dot(firstEdge);
        var firstDot = 2 * relativeControl.Dot(firstEdge) + relativeStart.Dot(lastEdge);
        var lastDot = relativeEnd.Dot(firstEdge) + 2 * relativeControl.Dot(lastEdge);
        var endDot = relativeEnd.Dot(lastEdge);
        if (startDot >= 0 && firstDot >= 0 && lastDot >= 0 && endDot >= 0 ||
            startDot <= 0 && firstDot <= 0 && lastDot <= 0 && endDot <= 0)
        {
            return nearestSquared;
        }

        // Use the nearer end as the parameter origin for each half. This avoids cancellation
        // in the power basis and preserves roots very close to either endpoint.
        if (from < .5 && to > .5)
        {
            var middle = BezierMath.Quadratic(relativeStart, relativeControl, relativeEnd, .5);
            nearestSquared = Math.Min(nearestSquared, middle.LengthSquared());
        }
        if (from < .5)
        {
            nearestSquared = CheckQuadraticStationaryPoints(relativeStart, relativeControl, relativeEnd,
                firstEdge, quadratic, from, Math.Min(to, .5), nearestSquared);
        }

        if (to > .5)
        {
            nearestSquared = CheckQuadraticStationaryPoints(relativeEnd, relativeControl, relativeStart,
                -lastEdge, quadratic, 1 - to, Math.Min(1 - from, .5), nearestSquared);
        }

        return nearestSquared;
    }

    private static double CheckQuadraticStationaryPoints(Vector2d start, Vector2d control, Vector2d end,
        Vector2d firstEdge, Vector2d quadratic, double from, double to, double nearestSquared)
    {
        // B(t) - point = start + 2 * firstEdge * t + quadratic * t².
        // The squared distance derivative vanishes where (B - point) · (B' / 2) = 0.
        var cubic = quadratic.LengthSquared();
        var squared = 3 * firstEdge.Dot(quadratic);
        var linear = 2 * firstEdge.LengthSquared() + start.Dot(quadratic);
        var constant = start.Dot(firstEdge);
        if (!double.IsFinite(cubic + linear + constant)) return double.NaN;

        Span<double> roots = stackalloc double[3];
        var count = PolynomialRoots.SolveCubic(cubic, squared, linear, constant, from, to, roots);
        for (var i = 0; i < count; i++)
        {
            var candidate = BezierMath.Quadratic(start, control, end, roots[i]);
            nearestSquared = Math.Min(nearestSquared, candidate.LengthSquared());
        }
        return nearestSquared;
    }

    private static double SegmentDistanceSquared(Vector2d point, Vector2d start, Vector2d end, double from, double to)
    {
        var edge = end - start;
        var relative = point - start;
        var lengthSquared = edge.LengthSquared();
        if (lengthSquared == 0) return relative.LengthSquared();

        var projection = relative.Dot(edge);
        if (projection <= .5 * lengthSquared)
        {
            var amount = Math.Clamp(projection / lengthSquared, from, to);
            return (relative - edge * amount).LengthSquared();
        }
        var fromEnd = point - end;
        var reverseAmount = Math.Clamp(-fromEnd.Dot(edge) / lengthSquared, 1 - to, 1 - from);
        return (fromEnd + edge * reverseAmount).LengthSquared();
    }

    /// <summary>Polyline approximation in parameter space; callers can increase segments for tighter results.</summary>
    public static float SampledDistance<TCurve>(Vector2f point, TCurve curve, int segmentCount) where TCurve : ICurve2D
    {
        Ensure.ThrowIfNull(curve);
        Ensure.Positive(segmentCount);
        return SampledDistance(point, curve, segmentCount, 0, 1);
    }

    private static float SampledDistance<TCurve>(Vector2f point, TCurve curve, int segmentCount, double from, double to) where TCurve : ICurve2D
    {
        var previous = curve.Evaluate((float)from);
        var nearestSquared = float.PositiveInfinity;
        for (var i = 1; i <= segmentCount; i++)
        {
            var next = curve.Evaluate((float)Interval.Lerp(from, to, i / (double)segmentCount));
            nearestSquared = MathF.Min(nearestSquared, Distance2D.DistanceSquaredToSegment(point, previous, next));
            previous = next;
        }
        return MathF.Sqrt(nearestSquared);
    }
}
