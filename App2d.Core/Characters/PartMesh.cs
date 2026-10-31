using App2d.Core.Mathematics;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Validation;
using System.Runtime.InteropServices;

namespace App2d.Core.Characters;

/// <summary>Local mesh geometry. Vertex Z and extrusion thickness are physical distances, never drawing layers.</summary>
public sealed record PartMesh
{
    public List<Vector3f> Vertices { get; set; } = [];
    public List<int> Triangles { get; set; } = [];
    public List<Vector3f>? Outline { get; set; }
    public float Thickness { get; set; }

    public void Rebuild()
    {
        if (Outline is not { } outline) return;
        if (outline.Count is < 3 or > 256)
            throw new InvalidDataException("A mesh outline needs 3 to 256 points.");
        new Limit(.0001f, 32).Check(Thickness, "mesh.thickness");
        foreach (var point in outline) CheckPoint(point, "mesh.outline");
        var mesh = Extrude(outline, Thickness);
        Vertices = mesh.Vertices;
        Triangles = mesh.Triangles;
    }

    public void Validate()
    {
        Rebuild();
        if (Vertices is not { Count: >= 3 } || Triangles is not { Count: >= 3 } || Triangles.Count % 3 != 0)
            throw new InvalidDataException("A mesh needs indexed triangles.");
        if (Vertices.Count > 32768 || Triangles.Count > 196608) throw new InvalidDataException("Mesh is too large.");
        foreach (var point in Vertices) CheckPoint(point, "mesh.vertex");
        if (Triangles.Any(i => i < 0 || i >= Vertices.Count))
            throw new InvalidDataException("Mesh triangle index outside vertices.");
        foreach (var (a, b, c) in MeshTopology.Triangles(CollectionsMarshal.AsSpan(Triangles)))
        {
            if (new Triangle3D(Vertices[a].ToVector3(), Vertices[b].ToVector3(), Vertices[c].ToVector3()).DoubleArea <= 1e-8d)
                throw new InvalidDataException("Mesh has a degenerate triangle.");
        }
    }
    /// <summary>Extrudes an XY outline around its physical Z; the resulting geometry owns no material.</summary>
    public static PartMesh Extrude(IEnumerable<Vector3f> outline, float thickness)
    {
        var points = outline.ToList();
        ArgGuard.ThrowIf(points.Count < 3 || thickness <= 0, "An extrusion needs an outline and positive thickness.");
        var area = PolygonGeometry2D.SignedAreaTwiceDouble([.. points.Select(p => p.XY.ToVector2())]);
        if (area < 0)
        {
            points.Reverse();
        }

        TriangleMesh2D triangles;
        try
        {
            triangles = PolygonTriangulation2D.Triangulate(points.Select(p => p.XY.ToVector2()), 1e-8);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("The mesh outline must be simple and have nonzero area.", ex);
        }
        var n = points.Count;
        var mesh = new PartMesh { Outline = [.. points], Thickness = thickness };
        mesh.Vertices.AddRange(points.Select(p => p with { Z = p.Z - thickness / 2 }));
        mesh.Vertices.AddRange(points.Select(p => p with { Z = p.Z + thickness / 2 }));
        foreach (var (a, b, c) in MeshTopology.Triangles(triangles.Indices))
        {
            mesh.Triangles.AddRange([a, c, b, n + a, n + b, n + c]);
        }
        foreach (var (i, j) in MeshTopology.Edges(n))
        {
            mesh.Triangles.AddRange([i, j, n + j, i, n + j, n + i]);
        }
        return mesh;
    }

    public static void CheckPoint(Vector3f point, string field)
    {
        var range = new Limit(-10000, 10000);
        range.Check(point.X, field + ".x");
        range.Check(point.Y, field + ".y");
        range.Check(point.Z, field + ".z");
    }
}
