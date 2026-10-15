using App2d.Core.Mathematics;
using App2d.Core.Meshes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Triangulates simple polygon perimeters into indexed mesh data.</summary>
public static class PolygonTriangulation2D
{
    /// <summary>Ear-clips a simple, unclosed polygon. Indices refer to the input vertices and retain its winding.</summary>
    public static TriangleMesh2D Triangulate(IEnumerable<Vector2> perimeter, double tolerance = 0)
    {
        ArgGuard.ThrowIfNull(perimeter);
        ArgGuard.ThrowIf(!double.IsFinite(tolerance) || tolerance < 0,
            "Tolerance must be finite and nonnegative.", nameof(tolerance));
        Vector2[] points = [.. perimeter];
        ArgGuard.ThrowIf(points.Length < 3, "A polygon needs at least three vertices.", nameof(perimeter));
        foreach (var point in points) ArgGuard.ThrowIfNotFinite(point, nameof(perimeter));
        var twiceArea = PolygonGeometry2D.SignedAreaTwiceDouble(points);
        ArgGuard.ThrowIf(Math.Abs(twiceArea) <= tolerance, "The polygon needs nonzero area.", nameof(perimeter));
        var winding = Math.Sign(twiceArea);
        var remaining = Enumerable.Range(0, points.Length).ToList();
        var indices = new List<int>((points.Length - 2) * 3);
        while (remaining.Count > 3)
        {
            var found = false;
            for (var i = 0; i < remaining.Count; i++)
            {
                var a = remaining[(i + remaining.Count - 1) % remaining.Count];
                var b = remaining[i];
                var c = remaining[(i + 1) % remaining.Count];
                if (winding * points[a].Orientation(points[b], points[c]) <= tolerance) continue;
                if (remaining.Any(j => j != a && j != b && j != c &&
                    winding * points[a].Orientation(points[b], points[j]) >= -tolerance &&
                    winding * points[b].Orientation(points[c], points[j]) >= -tolerance &&
                    winding * points[c].Orientation(points[a], points[j]) >= -tolerance))
                {
                    continue;
                }

                indices.AddRange([a, b, c]);
                remaining.RemoveAt(i);
                found = true;
                break;
            }
            ArgGuard.ThrowIf(!found, "The polygon must be simple and have nondegenerate ears.", nameof(perimeter));
        }
        ArgGuard.ThrowIf(winding * points[remaining[0]].Orientation(points[remaining[1]], points[remaining[2]]) <= tolerance,
            "The polygon has a degenerate final triangle.", nameof(perimeter));
        indices.AddRange(remaining);
        return new TriangleMesh2D(points, indices);
    }
}
