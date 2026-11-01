using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Meshes;

/// <summary>A triangle mesh as shared vertices and three indices per face, counter-clockwise seen from outside.</summary>
public sealed record IndexedMesh3D(IReadOnlyList<Vector3> Vertices, IReadOnlyList<int> Triangles)
{
    public int FaceCount => Triangles.Count / 3;

    /// <summary>Rejects missing faces, indices outside the vertices and degenerate triangles.</summary>
    public void Validate()
    {
        if (Vertices.Count < 3 || Triangles.Count < 3 || Triangles.Count % 3 != 0)
            throw new InvalidDataException("A mesh needs indexed triangles.");
        if (Triangles.Any(i => i < 0 || i >= Vertices.Count))
            throw new InvalidDataException("Mesh triangle index outside vertices.");
        for (var i = 0; i < Triangles.Count; i += 3)
        {
            Ensure.GreaterThan(new Triangle3D(Vertices[Triangles[i]], Vertices[Triangles[i + 1]], Vertices[Triangles[i + 2]]).DoubleArea, 1e-8d);
        }
    }
}

/// <summary>Closed solids as indexed meshes, the 3D counterpart of shape outline vertices.</summary>
public static class MeshGenerators3D
{
    /// <summary>
    /// Extrudes a simple XY outline symmetrically about each point's Z: the first ring is the back (Z - thickness / 2), the second
    /// the front. A clockwise outline is reversed first, so rings follow the counter-clockwise outline.
    /// </summary>
    public static IndexedMesh3D Extrude(IReadOnlyList<Vector3> outline, float thickness)
    {
        Ensure.ThrowIf(outline.Count < 3, "An extrusion needs an outline.");
        Ensure.Positive(thickness);
        var points = CounterClockwise(outline);
        var triangles = PolygonTriangulation2D.Triangulate(points.Select(p => new Vector2(p.X, p.Y)), 1e-8);
        var n = points.Count;
        var vertices = new List<Vector3>(n * 2);
        vertices.AddRange(points.Select(p => p with { Z = p.Z - thickness / 2 }));
        vertices.AddRange(points.Select(p => p with { Z = p.Z + thickness / 2 }));
        var indices = new List<int>();
        foreach (var (a, b, c) in MeshTopology.Triangles(triangles.Indices))
        {
            indices.AddRange([a, c, b, n + a, n + b, n + c]);
        }
        foreach (var (i, j) in MeshTopology.Edges(n))
        {
            indices.AddRange([i, j, n + j, i, n + j, n + i]);
        }
        return new(vertices, indices);
    }

    /// <summary>The outline in counter-clockwise XY order: itself when it already is, otherwise reversed.</summary>
    public static List<Vector3> CounterClockwise(IEnumerable<Vector3> outline)
    {
        var points = outline.ToList();
        if (PolygonGeometry2D.SignedAreaTwiceDouble([.. points.Select(p => new Vector2(p.X, p.Y))]) < 0)
        {
            points.Reverse();
        }

        return points;
    }

    /// <summary>An axis-aligned box about the origin. Faces come in pairs of triangles: -Z, +Z, -X, +X, +Y, -Y.</summary>
    public static IndexedMesh3D Box(Vector3 halfExtents)
    {
        var (x, y, z) = (halfExtents.X, halfExtents.Y, halfExtents.Z);
        Vector3[] corners = [new(-x, -y, -z), new(x, -y, -z), new(x, y, -z), new(-x, y, -z), new(-x, -y, z), new(x, -y, z), new(x, y, z), new(-x, y, z)];
        return new(corners, [0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7, 0, 4, 7, 0, 7, 3, 1, 2, 6, 1, 6, 5, 3, 7, 6, 3, 6, 2, 0, 1, 5, 0, 5, 4]);
    }
}
