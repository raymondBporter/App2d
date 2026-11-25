using App2d.Core.Geometry.Curves;
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

    internal static float DistanceOnInterval<TCurve>(Vector2f point, TCurve curve, double from, double to) where TCurve : ICurve2D
    {
        if (from == to) return (float)(curve.Evaluate((float)from).ToVector2d() - point.ToVector2d()).Length();
        if (from == 0 && to == 1) return curve.Distance(point);

        switch (curve)
        {
            case LineSegmentCurve2D line:
                return (float)Math.Sqrt(SegmentDistanceSquared(point.ToVector2d(), line.Start.ToVector2d(), line.End.ToVector2d(), from, to));
            case Arc2D arc:
                return arc.Distance(point, from, to);
            case QuadraticBezier2D quadratic:
                return quadratic.Distance(point, from, to);
            case PolylineCurve2D polyline:
                return PolylineDistance(point.ToVector2d(), polyline.Points, from, to);
            case RoundedPolylineCurve2D rounded:
                return rounded.Distance(point, from, to);
            default:
                return SampledDistance(point, curve, 128, from, to);
        }
    }

    private static float PolylineDistance(Vector2d point, ReadOnlySpan<Vector2f> points, double from, double to)
    {
        var segmentCount = points.Length - 1;
        var first = from * segmentCount;
        var last = to * segmentCount;
        var nearestSquared = double.PositiveInfinity;
        for (var i = (int)first; i < segmentCount && i <= last; i++)
        {
            nearestSquared = Math.Min(nearestSquared, SegmentDistanceSquared(point,
                points[i].ToVector2d(), points[i + 1].ToVector2d(), Math.Max(0, first - i), Math.Min(1, last - i)));
        }
        return (float)Math.Sqrt(nearestSquared);
    }

    internal static float RoundedSpanDistance(Vector2d point, PolynomialCurveSpan2D[] spans, double from, double to,
        double nearestSquared)
    {
        foreach (var span in spans)
        {
            if (span.From > to) break;
            var low = Math.Max(from, span.From);
            var high = Math.Min(to, span.To);
            if (low > high) continue;
            if (ControlBoundsDistanceSquared(point, span.Controls) >= nearestSquared) continue;

            var width = span.To - span.From;
            var localFrom = (low - span.From) / width;
            var localTo = (high - span.From) / width;
            var controls = span.Controls;
            var distanceSquared = controls.Length == 2
                ? SegmentDistanceSquared(point, controls[0], controls[1], localFrom, localTo)
                : QuadraticDistanceSquared(point, controls[0], controls[1], controls[2], localFrom, localTo);
            nearestSquared = Math.Min(nearestSquared, distanceSquared);
            if (nearestSquared == 0) break;
        }
        return (float)Math.Sqrt(nearestSquared);
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
            return nearestSquared;

        // Use the nearer end as the parameter origin for each half. This avoids cancellation
        // in the power basis and preserves roots very close to either endpoint.
        if (from < .5 && to > .5)
        {
            var middle = BezierMath.Quadratic(relativeStart, relativeControl, relativeEnd, .5);
            nearestSquared = Math.Min(nearestSquared, middle.LengthSquared());
        }
        if (from < .5)
            nearestSquared = CheckQuadraticStationaryPoints(relativeStart, relativeControl, relativeEnd,
                firstEdge, quadratic, from, Math.Min(to, .5), nearestSquared);
        if (to > .5)
            nearestSquared = CheckQuadraticStationaryPoints(relativeEnd, relativeControl, relativeStart,
                -lastEdge, quadratic, 1 - to, Math.Min(1 - from, .5), nearestSquared);
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
