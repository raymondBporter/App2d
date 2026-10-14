using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Meshes;

/// <summary>An indexed set of filled triangles in XY, independent of rendering and authoring.</summary>
public sealed class TriangleMesh2D
{
    private readonly Vector2[] _vertices;
    private readonly int[] _indices;

    public TriangleMesh2D(IEnumerable<Vector2> vertices, IEnumerable<int> indices)
    {
        ArgGuard.ThrowIfNull(vertices);
        ArgGuard.ThrowIfNull(indices);
        _vertices = [.. vertices];
        _indices = [.. indices];
        ArgGuard.ThrowIf(_vertices.Length < 3, "A triangle mesh needs at least three vertices.", nameof(vertices));
        ArgGuard.ThrowIf(_indices.Length == 0 || _indices.Length % 3 != 0,
            "Triangle indices must form complete triples.", nameof(indices));
        foreach (var vertex in _vertices) ArgGuard.ThrowIfNotFinite(vertex, nameof(vertices));
        foreach (var index in _indices)
            ArgGuard.ThrowIf(index < 0 || index >= _vertices.Length, "Triangle index is outside the vertices.", nameof(indices));

        var area = 0d;
        for (var i = 0; i < _indices.Length; i += 3)
        {
            var twice = _vertices[_indices[i]].Orientation(_vertices[_indices[i + 1]], _vertices[_indices[i + 2]]);
            ArgGuard.ThrowIf(twice == 0, "Triangle vertices must not be collinear.", nameof(indices));
            area += Math.Abs(twice) / 2;
        }
        ArgGuard.ThrowIf(area > float.MaxValue, "Triangle mesh area is too large.", nameof(vertices));
        Area = (float)area;
        Bounds = Rect2D.FromPoints(_vertices);
    }

    public ReadOnlySpan<Vector2> Vertices => _vertices;
    public ReadOnlySpan<int> Indices => _indices;
    public int TriangleCount => _indices.Length / 3;
    /// <summary>The sum of triangle areas; overlaps are counted once per triangle.</summary>
    public float Area { get; }
    public Rect2D Bounds { get; }

    public (Vector2 A, Vector2 B, Vector2 C) TriangleAt(int index)
    {
        if ((uint)index >= (uint)TriangleCount) throw new ArgumentOutOfRangeException(nameof(index));
        var i = index * 3;
        return (_vertices[_indices[i]], _vertices[_indices[i + 1]], _vertices[_indices[i + 2]]);
    }

    /// <summary>Contains a point when it lies in any triangle, including an edge.</summary>
    public bool ContainsPoint(Vector2 point, double edgeTolerance = 0)
    {
        ArgGuard.ThrowIfNotFinite(point);
        ArgGuard.ThrowIf(!double.IsFinite(edgeTolerance) || edgeTolerance < 0,
            "Edge tolerance must be finite and nonnegative.", nameof(edgeTolerance));
        if (edgeTolerance == 0 && !Bounds2DContains(point)) return false;
        for (var i = 0; i < TriangleCount; i++)
        {
            var (a, b, c) = TriangleAt(i);
            var sign = Math.Sign(a.Orientation(b, c));
            if (sign * a.Orientation(b, point) >= -edgeTolerance &&
                sign * b.Orientation(c, point) >= -edgeTolerance &&
                sign * c.Orientation(a, point) >= -edgeTolerance)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>Turns the filled triangles into a non-convex collision shape without filling concavities.</summary>
    /// <remarks>Collision resolves per triangle, so dynamic bodies may encounter internal edges.</remarks>
    public CompositeShape2D ToCompositeShape() => new(
        Enumerable.Range(0, TriangleCount).Select(index =>
        {
            var (a, b, c) = TriangleAt(index);
            return (IConvexShape2D)new Triangle2D(a, b, c);
        }));

    private bool Bounds2DContains(Vector2 point) =>
        point.X >= Bounds.Min.X && point.X <= Bounds.Max.X && point.Y >= Bounds.Min.Y && point.Y <= Bounds.Max.Y;

    /// <summary>Ear-clips a simple, unclosed polygon. Indices refer to the input vertices and retain its winding.</summary>
    public static TriangleMesh2D TriangulateSimplePolygon(IEnumerable<Vector2> perimeter, double tolerance = 0)
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
