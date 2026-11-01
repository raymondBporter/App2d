using App2d.Core.Geometry.Meshes;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.Polygons;

/// <summary>Triangulates simple polygon perimeters into indexed mesh data.</summary>
public static class PolygonTriangulation2D
{
    /// <summary>Ear-clips a simple, unclosed polygon. Indices refer to the input vertices and retain its winding.</summary>
    public static TriangleMesh2D Triangulate(IEnumerable<Vector2> perimeter, double tolerance = 0)
    {
        Ensure.ThrowIfNull(perimeter);
        Ensure.NonNegative(tolerance);
        Vector2[] points = [.. perimeter];
        Ensure.ThrowIf(points.Length < 3, "A polygon needs at least three vertices.", nameof(perimeter));
        foreach (var point in points)
        {
            Ensure.Finite(point, nameof(perimeter));
        }

        var twiceArea = PolygonGeometry2D.SignedAreaTwiceDouble(points);
        Ensure.GreaterThan(Math.Abs(twiceArea), tolerance);
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
                var first = new Vector2d(points[a]);
                var second = new Vector2d(points[b]);
                var third = new Vector2d(points[c]);
                if (winding * first.Orientation(second, third) <= tolerance)
                {
                    continue;
                }

                if (remaining.Any(j => j != a && j != b && j != c &&
                    winding * first.Orientation(second, new(points[j])) >= -tolerance &&
                    winding * second.Orientation(third, new(points[j])) >= -tolerance &&
                    winding * third.Orientation(first, new(points[j])) >= -tolerance))
                {
                    continue;
                }

                indices.AddRange([a, b, c]);
                remaining.RemoveAt(i);
                found = true;
                break;
            }
            Ensure.ThrowIf(!found, "The polygon must be simple and have nondegenerate ears.", nameof(perimeter));
        }
        Ensure.GreaterThan(winding * new Vector2d(points[remaining[0]]).Orientation(new(points[remaining[1]]), new(points[remaining[2]])), tolerance);
        indices.AddRange(remaining);
        return new TriangleMesh2D(points, indices);
    }
}
