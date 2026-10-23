using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.Intersection;

/// <summary>
/// Nearest isolated ray/curve intersections on the actual curve, without polyline sampling.
/// Normals point counterclockwise from increasing curve progress. Collinear overlap and point curves miss.
/// Custom/procedural curves without a supported representation throw NotSupportedException.
/// </summary>
public static class CurveRaycast2D
{
    /// <summary>Queries a curve using a unit world/local ray; the hit distance uses the ray's coordinate space.</summary>
    public static bool TryIntersect(Ray2D ray, ICurve2D curve, float maxDistance, out RayHit2D hit)
    {
        ray.Validate();
        return TryIntersect(ray.Origin, ray.Direction, curve, maxDistance, out hit);
    }

    /// <summary>Queries a curve with any finite nonzero direction; Distance is the original ray parameter.</summary>
    public static bool TryIntersect(Vector2 origin, Vector2 direction, ICurve2D curve, float maxDistance, out RayHit2D hit)
    {
        ArgGuard.ThrowIfNull(curve);
        ArgGuard.Finite(origin);
        ArgGuard.Finite(direction);
        ArgGuard.ThrowIf(direction == Vector2.Zero, "Ray direction must be nonzero.", nameof(direction));
        ArgGuard.NonNegative(maxDistance, allowInfinity: true);
        return Query(origin, direction, curve, 0, 1, maxDistance, out hit);
    }

    private static bool Query(Vector2 origin, Vector2 direction, ICurve2D curve, double from, double to,
        float maxDistance, out RayHit2D hit)
    {
        hit = default;
        if (from == to) return false;
        switch (curve)
        {
            case ICurveSlice2D slice:
                var width = (double)slice.To - slice.From;
                return Query(origin, direction, slice.SourceCurve, slice.From + width * from, slice.From + width * to, maxDistance, out hit);
            case Arc2D arc:
                return Raycast2D.TryArc(origin, direction, arc.Center, arc.Radius,
                    arc.StartAngleRadians + arc.SweepAngleRadians * from, arc.SweepAngleRadians * (to - from), maxDistance, out hit);
            case LineSegmentCurve2D line:
                return Polynomial(origin, direction, [line.Start.ToVector2d(), line.End.ToVector2d()], from, to, maxDistance, out hit);
            case QuadraticBezier2D quadratic:
                return Polynomial(origin, direction, [quadratic.Start.ToVector2d(), quadratic.Control.ToVector2d(), quadratic.End.ToVector2d()], from, to, maxDistance, out hit);
            case CubicBezier2D cubic:
                return Polynomial(origin, direction, [cubic.Start.ToVector2d(), cubic.Control1.ToVector2d(), cubic.Control2.ToVector2d(), cubic.End.ToVector2d()], from, to, maxDistance, out hit);
            case PolylineCurve2D polyline:
                var found = false;
                var count = polyline.Points.Length - 1;
                Span<Vector2d> points = stackalloc Vector2d[2];
                for (var i = 0; i < count; i++)
                {
                    var low = Math.Max(0, from * count - i);
                    var high = Math.Min(1, to * count - i);
                    if (low > high) continue;
                    points[0] = polyline.Points[i].ToVector2d();
                    points[1] = polyline.Points[i + 1].ToVector2d();
                    if (Polynomial(origin, direction, points, low, high, maxDistance, out var candidate) && (!found || candidate.Distance < hit.Distance))
                    { hit = candidate; maxDistance = hit.Distance; found = true; }
                }
                return found;
            case BSpline2D spline:
                return Spans(origin, direction, spline.GetPolynomialSpans(), from, to, maxDistance, out hit);
            case RoundedPolylineCurve2D rounded:
                return Spans(origin, direction, rounded.GetPolynomialSpans(), from, to, maxDistance, out hit);
            default:
                throw new NotSupportedException($"Direct ray intersections are not available for {curve.GetType().Name}. No sampled fallback is applied.");
        }
    }

    private static bool Spans(Vector2 origin, Vector2 direction, PolynomialCurveSpan2D[] spans,
        double from, double to, float maxDistance, out RayHit2D hit)
    {
        hit = default;
        var found = false;
        foreach (var span in spans)
        {
            var low = Math.Max(from, span.From);
            var high = Math.Min(to, span.To);
            if (low > high) continue;
            var width = span.To - span.From;
            if (Polynomial(origin, direction, span.Controls, (low - span.From) / width, (high - span.From) / width, maxDistance, out var candidate)
                && (!found || candidate.Distance < hit.Distance))
            { hit = candidate; maxDistance = hit.Distance; found = true; }
        }
        return found;
    }

    private static bool Polynomial(Vector2 origin, Vector2 direction, ReadOnlySpan<Vector2d> controls,
        double from, double to, float maxDistance, out RayHit2D hit)
    {
        hit = default;
        var axis = direction.ToVector2d();
        var rayOrigin = origin.ToVector2d();
        var axisSquared = axis.LengthSquared();
        Span<double> crossings = controls.Length <= 32 ? stackalloc double[controls.Length] : new double[controls.Length];
        var minimumSide = double.PositiveInfinity;
        var maximumSide = double.NegativeInfinity;
        var minimumAlong = double.PositiveInfinity;
        var maximumAlong = double.NegativeInfinity;
        for (var i = 0; i < controls.Length; i++)
        {
            var relative = controls[i] - rayOrigin;
            crossings[i] = relative.Cross(axis);
            minimumSide = Math.Min(minimumSide, crossings[i]);
            maximumSide = Math.Max(maximumSide, crossings[i]);
            var along = relative.Dot(axis) / axisSquared;
            minimumAlong = Math.Min(minimumAlong, along);
            maximumAlong = Math.Max(maximumAlong, along);
        }
        // A Bezier lies inside its control hull. These bounds reject whole spans before solving.
        if (minimumSide > 0 || maximumSide < 0 || maximumAlong < 0 || minimumAlong > maxDistance) return false;
        Span<double> roots = controls.Length <= 32 ? stackalloc double[controls.Length - 1] : new double[controls.Length - 1];
        var count = BernsteinRoots2D.Find(crossings, from, to, roots);
        var found = false;
        for (var i = 0; i < count; i++)
        {
            var point = Evaluate(controls, roots[i]);
            var distance = (point - rayOrigin).Dot(axis) / axisSquared;
            if (!double.IsFinite(distance) || distance < 0 || distance > float.MaxValue || distance > maxDistance || found && distance >= hit.Distance) continue;
            var tangent = Tangent(controls, roots[i]);
            var length = tangent.Length();
            if (length == 0) continue;
            hit = new((rayOrigin + axis * distance).ToVector2(), (tangent.PerpCcw / length).ToVector2(), (float)distance);
            maxDistance = hit.Distance;
            found = true;
        }
        return found;
    }

    private static Vector2d Evaluate(ReadOnlySpan<Vector2d> controls, double amount)
    {
        Span<Vector2d> work = controls.Length <= 32 ? stackalloc Vector2d[controls.Length] : new Vector2d[controls.Length];
        controls.CopyTo(work);
        for (var length = work.Length - 1; length > 0; length--)
            for (var i = 0; i < length; i++) work[i] = work[i] * (1 - amount) + work[i + 1] * amount;
        return work[0];
    }

    private static Vector2d Tangent(ReadOnlySpan<Vector2d> controls, double amount)
    {
        Span<Vector2d> derivatives = controls.Length <= 32 ? stackalloc Vector2d[controls.Length] : new Vector2d[controls.Length];
        controls.CopyTo(derivatives);
        for (var count = controls.Length - 1; count > 0; count--)
        {
            for (var i = 0; i < count; i++) derivatives[i] = derivatives[i + 1] - derivatives[i];
            var derivative = Evaluate(derivatives[..count], amount);
            if (derivative.LengthSquared() > 0)
            {
                // At a cusp use the first nonzero one-sided derivative. At u=1 use the incoming side.
                var order = controls.Length - count;
                return amount == 1 && (order & 1) == 0 ? -derivative : derivative;
            }
        }
        return Vector2d.Zero;
    }
}
