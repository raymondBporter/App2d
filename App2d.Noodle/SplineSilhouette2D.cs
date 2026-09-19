using App2d.Core.Curves;
using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Noodle;

/// <summary>
/// Small cubic-Bezier art factory for the SplineMan prototype. The curves are
/// sampled into convex render geometry; the resulting pieces stay rigid and
/// continue to use the exact same bones and animation data as every other skin.
/// </summary>
internal static class SplineSilhouette2D
{
    private const float Kappa = 0.55228475f;
    private const int SamplesPerCurve = 6;

    public static ConvexPolygon2D CreateLimb(
        float length,
        float rootRadius,
        float tipRadius,
        float overlap)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rootRadius);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tipRadius);
        ArgumentOutOfRangeException.ThrowIfNegative(overlap);

        var startX = -overlap;
        var endX = length + overlap;
        var span = endX - startX;
        var path = new CubicPathBuilder2D(new Vector2(startX, rootRadius));

        path.CurveTo(
            new Vector2(startX + span * 0.31f, rootRadius * 1.04f),
            new Vector2(endX - span * 0.34f, tipRadius * 1.08f),
            new Vector2(endX, tipRadius));
        AddRightCap(path, endX, tipRadius);
        path.CurveTo(
            new Vector2(endX - span * 0.34f, -tipRadius * 1.08f),
            new Vector2(startX + span * 0.31f, -rootRadius * 1.04f),
            new Vector2(startX, -rootRadius));
        AddLeftCap(path, startX, rootRadius);

        return path.BuildConvex();
    }

    public static ConvexPolygon2D CreateTorso(float length)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(length);
        var startX = -8f;
        var endX = length + 9f;
        const float hipRadius = 27f;
        const float shoulderRadius = 42f;
        var span = endX - startX;
        var path = new CubicPathBuilder2D(new Vector2(startX, hipRadius));

        path.CurveTo(
            new Vector2(startX + span * 0.28f, hipRadius + 1f),
            new Vector2(endX - span * 0.31f, shoulderRadius - 4f),
            new Vector2(endX, shoulderRadius));
        AddRightCap(path, endX, shoulderRadius);
        path.CurveTo(
            new Vector2(endX - span * 0.31f, -shoulderRadius + 4f),
            new Vector2(startX + span * 0.28f, -hipRadius - 1f),
            new Vector2(startX, -hipRadius));
        AddLeftCap(path, startX, hipRadius);

        return path.BuildConvex();
    }

    public static ConvexPolygon2D CreateHead(float radius) =>
        CreateEllipse(radius * 0.88f, radius * 1.08f);

    private static ConvexPolygon2D CreateEllipse(float radiusX, float radiusY)
    {
        var path = new CubicPathBuilder2D(new Vector2(radiusX, 0f));
        path.CurveTo(
            new Vector2(radiusX, Kappa * radiusY),
            new Vector2(Kappa * radiusX, radiusY),
            new Vector2(0f, radiusY));
        path.CurveTo(
            new Vector2(-Kappa * radiusX, radiusY),
            new Vector2(-radiusX, Kappa * radiusY),
            new Vector2(-radiusX, 0f));
        path.CurveTo(
            new Vector2(-radiusX, -Kappa * radiusY),
            new Vector2(-Kappa * radiusX, -radiusY),
            new Vector2(0f, -radiusY));
        path.CurveTo(
            new Vector2(Kappa * radiusX, -radiusY),
            new Vector2(radiusX, -Kappa * radiusY),
            new Vector2(radiusX, 0f));
        return path.BuildConvex();
    }

    private static void AddRightCap(CubicPathBuilder2D path, float centerX, float radius)
    {
        path.CurveTo(
            new Vector2(centerX + Kappa * radius, radius),
            new Vector2(centerX + radius, Kappa * radius),
            new Vector2(centerX + radius, 0f));
        path.CurveTo(
            new Vector2(centerX + radius, -Kappa * radius),
            new Vector2(centerX + Kappa * radius, -radius),
            new Vector2(centerX, -radius));
    }

    private static void AddLeftCap(CubicPathBuilder2D path, float centerX, float radius)
    {
        path.CurveTo(
            new Vector2(centerX - Kappa * radius, -radius),
            new Vector2(centerX - radius, -Kappa * radius),
            new Vector2(centerX - radius, 0f));
        path.CurveTo(
            new Vector2(centerX - radius, Kappa * radius),
            new Vector2(centerX - Kappa * radius, radius),
            new Vector2(centerX, radius));
    }

    private sealed class CubicPathBuilder2D(Vector2 start)
    {
        private readonly List<Vector2> _points = [start];
        private readonly Vector2 _start = start;
        private Vector2 _current = start;

        public void CurveTo(Vector2 control1, Vector2 control2, Vector2 end)
        {
            var curve = new CubicBezier2D(_current, control1, control2, end);
            for (var sample = 1; sample <= SamplesPerCurve; sample++)
            {
                var amount = sample / (float)SamplesPerCurve;
                _points.Add(curve.Evaluate(amount));
            }
            _current = end;
        }

        public ConvexPolygon2D BuildConvex()
        {
            if (Vector2.DistanceSquared(_points[^1], _start) <= 0.0001f)
                _points.RemoveAt(_points.Count - 1);
            return new ConvexPolygon2D(CreateConvexHull(_points));
        }

        private static List<Vector2> CreateConvexHull(IEnumerable<Vector2> source)
        {
            var points = source
                .Distinct()
                .OrderBy(point => point.X)
                .ThenBy(point => point.Y)
                .ToArray();
            if (points.Length < 3)
                throw new InvalidOperationException("A closed spline silhouette needs at least three distinct points.");

            var hull = new List<Vector2>(points.Length * 2);
            foreach (var point in points)
            {
                while (hull.Count >= 2 && Cross(hull[^2], hull[^1], point) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }

            var lowerCount = hull.Count;
            for (var index = points.Length - 2; index >= 0; index--)
            {
                var point = points[index];
                while (hull.Count > lowerCount && Cross(hull[^2], hull[^1], point) <= 0f)
                    hull.RemoveAt(hull.Count - 1);
                hull.Add(point);
            }

            hull.RemoveAt(hull.Count - 1);
            return hull;
        }

        private static float Cross(Vector2 origin, Vector2 a, Vector2 b)
        {
            var first = a - origin;
            var second = b - origin;
            return first.X * second.Y - first.Y * second.X;
        }
    }
}
