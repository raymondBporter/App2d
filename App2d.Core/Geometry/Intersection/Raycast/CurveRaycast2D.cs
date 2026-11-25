using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics.RootFinding;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Intersection.Raycast;

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
    public static bool TryIntersect(Vector2f origin, Vector2f direction, ICurve2D curve, float maxDistance, out RayHit2D hit)
    {
        Ensure.ThrowIfNull(curve);
        Raycast2D.ValidateParameters(origin, direction, maxDistance);
        return TryIntersectInterval(origin, direction, curve, 0, 1, maxDistance, out hit);
    }

    /// <summary>Queries only the part of a curve between two ordered parameters in [0, 1]. Equal parameters never hit.</summary>
    public static bool TryIntersect(Vector2f origin, Vector2f direction, ICurve2D curve, float from, float to, float maxDistance, out RayHit2D hit)
    {
        Ensure.ThrowIfNull(curve);
        Ensure.InRange(from, 0f, 1f);
        Ensure.InRange(to, from, 1f);
        Raycast2D.ValidateParameters(origin, direction, maxDistance);
        return TryIntersectInterval(origin, direction, curve, from, to, maxDistance, out hit);
    }

    internal static bool TryIntersectInterval(Vector2f origin, Vector2f direction, ICurve2D curve, double from, double to,
        float maxDistance, out RayHit2D hit)
    {
        hit = default;
        if (from == to) return false;
        switch (curve)
        {
            case Arc2D arc:
                return Raycast2D.TryArc(origin, direction, arc.Center, arc.Radius,
                    arc.StartAngleRadians + arc.SweepAngleRadians * from, arc.SweepAngleRadians * (to - from), maxDistance, out hit);
            case IPolynomialCurve2D polynomial:
                return Spans(origin, direction, polynomial.GetPolynomialSpans(), from, to, maxDistance, out hit);
            default:
                throw new NotSupportedException($"Direct ray intersections are not available for {curve.GetType().Name}. No sampled fallback is applied.");
        }
    }

    // Every polynomial curve is its pieces: the nearest hit over the pieces inside [from, to].
    private static bool Spans(Vector2f origin, Vector2f direction, PolynomialSpans2D spans, double from, double to, float maxDistance, out RayHit2D hit)
    {
        hit = default;
        var found = false;
        Span<Vector2d> storage = spans.MaximumDegree < 32 ? stackalloc Vector2d[spans.MaximumDegree + 1] : new Vector2d[spans.MaximumDegree + 1];
        for (var i = 0; i < spans.Count; i++)
        {
            var range = spans.Get(i, storage);
            var low = Math.Max(from, range.From);
            var high = Math.Min(to, range.To);
            if (low > high) continue;
            var width = range.To - range.From;
            if (Polynomial(origin, direction, storage[..range.ControlCount], (low - range.From) / width, (high - range.From) / width, maxDistance, out var candidate)
                && (!found || candidate.Distance < hit.Distance))
            { hit = candidate; maxDistance = hit.Distance; found = true; }
        }
        return found;
    }

    private static bool Polynomial(Vector2f origin, Vector2f direction, ReadOnlySpan<Vector2d> controls,
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
        var count = BernsteinRoots.Find(crossings, from, to, roots);
        var found = false;
        for (var i = 0; i < count; i++)
        {
            var point = BezierMath.EvaluateControls(controls, roots[i]);
            var distance = (point - rayOrigin).Dot(axis) / axisSquared;
            if (!double.IsFinite(distance) || distance < 0 || distance > float.MaxValue || distance > maxDistance || found && distance >= hit.Distance) continue;
            var tangent = Tangent(controls, roots[i]);
            var length = tangent.Length();
            if (length == 0) continue;
            hit = new((rayOrigin + axis * distance).ToVector2f(), (tangent.PerpCcw / length).ToVector2f(), (float)distance);
            maxDistance = hit.Distance;
            found = true;
        }
        return found;
    }

    private static Vector2d Tangent(ReadOnlySpan<Vector2d> controls, double amount)
    {
        Span<Vector2d> derivatives = controls.Length <= 32 ? stackalloc Vector2d[controls.Length] : new Vector2d[controls.Length];
        controls.CopyTo(derivatives);
        for (var count = controls.Length - 1; count > 0; count--)
        {
            for (var i = 0; i < count; i++) derivatives[i] = derivatives[i + 1] - derivatives[i];
            var derivative = BezierMath.EvaluateControls(derivatives[..count], amount);
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
