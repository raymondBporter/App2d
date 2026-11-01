using App2d.Core.Geometry.Meshes;
using App2d.Core.Mathematics;
using App2d.Core.Validation;

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
        if (Vertices is null || Triangles is null) throw new InvalidDataException("A mesh needs indexed triangles.");
        if (Vertices.Count > 32768 || Triangles.Count > 196608) throw new InvalidDataException("Mesh is too large.");
        foreach (var point in Vertices) CheckPoint(point, "mesh.vertex");
        ToIndexedMesh().Validate();
    }

    /// <summary>The geometry as a plain indexed mesh, with no outline or thickness.</summary>
    public IndexedMesh3D ToIndexedMesh() => new([.. Vertices.Select(v => v.ToVector3())], Triangles);

    /// <summary>Extrudes an XY outline around its physical Z; the resulting geometry owns no material.</summary>
    public static PartMesh Extrude(IEnumerable<Vector3f> outline, float thickness)
    {
        var points = outline.Select(p => p.ToVector3()).ToList();
        ArgGuard.ThrowIf(points.Count < 3 || thickness <= 0, "An extrusion needs an outline and positive thickness.");
        points = MeshGenerators3D.CounterClockwise(points);
        IndexedMesh3D solid;
        try
        {
            solid = MeshGenerators3D.Extrude(points, thickness);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidDataException("The mesh outline must be simple and have nonzero area.", ex);
        }
        return new()
        {
            Outline = [.. points.Select(p => new Vector3f(p))],
            Thickness = thickness,
            Vertices = [.. solid.Vertices.Select(v => new Vector3f(v))],
            Triangles = [.. solid.Triangles],
        };
    }

    public static void CheckPoint(Vector3f point, string field)
    {
        var range = new Limit(-10000, 10000);
        range.Check(point.X, field + ".x");
        range.Check(point.Y, field + ".y");
        range.Check(point.Z, field + ".z");
    }
}
