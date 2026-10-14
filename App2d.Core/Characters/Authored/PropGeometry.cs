using App2d.Core.Geometry;
using App2d.Core.Meshes;
using App2d.Core.Validation;
using System.Globalization;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>Small solid-art helpers and a deliberately limited, triangulated OBJ interchange path. No runtime file dependencies.</summary>
public static class PropGeometry
{
    /// <summary>A tapered diamond-section blade. The ridge supplies broad-face highlights without painted detail.</summary>
    public static PropSolid Blade(float root, float shoulder, float tip, float halfWidth, float halfDepth, string fill)
    {
        var mesh = new PropSolid { Material = new() { Fill = fill, Outline = new() } };
        foreach (var (x, width) in new[] { (root, halfWidth), (shoulder, halfWidth * .72f) })
            mesh.Vertices.AddRange([new(x, -width, 0), new(x, 0, -halfDepth), new(x, width, 0), new(x, 0, halfDepth)]);
        mesh.Vertices.Add(new(tip, 0, 0));
        mesh.Triangles.AddRange([0, 2, 1, 0, 3, 2]);
        for (var i = 0; i < 4; i++)
        {
            var j = (i + 1) % 4;
            mesh.Triangles.AddRange([i, j, 4 + j, i, 4 + j, 4 + i, 4 + i, 4 + j, 8]);
        }
        return mesh;
    }

    /// <summary>Extrudes a simple XY outline, including concave silhouettes; either winding is accepted. Thickness is centered about Z.</summary>
    public static PropSolid Extrude(IEnumerable<PuppetPoint> outline, float thickness, string fill)
    {
        var points = outline.ToList();
        ArgGuard.ThrowIf(points.Count < 3 || thickness <= 0, "An extrusion needs an outline and positive thickness.");
        var area = PolygonGeometry2D.SignedAreaTwiceDouble([.. points.Select(p => p.XY)]);
        if (area < 0) points.Reverse();
        TriangleMesh2D triangles;
        try { triangles = TriangleMesh2D.TriangulateSimplePolygon(points.Select(p => p.XY), 1e-8); }
        catch (ArgumentException ex) { throw new InvalidDataException("The prop outline must be simple and have nonzero area.", ex); }
        var n = points.Count; var mesh = new PropSolid { Material = new() { Fill = fill, Outline = new() }, Outline = [.. points], Thickness = thickness };
        mesh.Vertices.AddRange(points.Select(p => p with { Z = p.Z - thickness / 2 }));
        mesh.Vertices.AddRange(points.Select(p => p with { Z = p.Z + thickness / 2 }));
        for (var i = 0; i < triangles.Indices.Length; i += 3)
        {
            var a = triangles.Indices[i]; var b = triangles.Indices[i + 1]; var c = triangles.Indices[i + 2];
            mesh.Triangles.AddRange([a, c, b, n + a, n + b, n + c]);
        }
        for (var i = 0; i < n; i++) { var j = (i + 1) % n; mesh.Triangles.AddRange([i, j, n + j, i, n + j, n + i]); }
        return mesh;
    }

    /// <summary>Turns the existing convex weapon silhouettes into simple solids, preserving their color and dimensions.</summary>
    public static PropAsset Solidify(PropAsset prop, float thickness, bool centerDepth = false)
    {
        foreach (var shape in prop.Shapes)
        {
            if (shape.Points.Count == 0) shape.RestorePoints();
            var points = shape.Points.ConvertAll(p => centerDepth ? p with { Z = 0 } : p);
            var fill = shape.RenderMaterial.Fill ?? prop.Ink;
            PropSolid Solid(IEnumerable<PuppetPoint> outline, float depth)
            {
                var solid = Extrude(outline, depth, fill);
                solid.Material = shape.RenderMaterial with { Fill = fill };
                return solid;
            }
            if (shape.IsFilled)
            {
                prop.Solids.Add(Solid(points, thickness));
            }
            else
            {
                for (var i = 1; i < points.Count; i++)
                {
                    var a = points[i - 1]; var b = points[i]; var d = Vector2.Normalize(b.XY - a.XY);
                    var off = new Vector3(-d.Y, d.X, 0) * shape.Width / 2;
                    prop.Solids.Add(Solid([PuppetPoint.From(a.XYZ - off), PuppetPoint.From(b.XYZ - off), PuppetPoint.From(b.XYZ + off), PuppetPoint.From(a.XYZ + off)], shape.Width * .85f));
                }
            }
        }
        prop.Shapes.Clear(); prop.LineWidth = .012f; prop.Validate(); return prop;
    }

    /// <summary>Loads positions and triangle faces, welding coincident vertices for clean outlines. UVs/normals/materials are ignored.
    /// The caller chooses scale, grip and color. Export with +X along the weapon and +Y across its broad face.</summary>
    public static PropSolid ImportObj(string text, float scale = 1, string fill = "#c8b18a")
    {
        if (!float.IsFinite(scale) || scale <= 0) throw new InvalidDataException("OBJ scale must be positive.");
        var positions = new List<int>(); var welded = new Dictionary<Vector3, int>(); var mesh = new PropSolid { Material = new() { Fill = fill, Outline = new() } };
        var lineNumber = 0;
        foreach (var line in text.Split('\n'))
        {
            lineNumber++;
            var words = line.Split('#')[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) continue;
            try
            {
                if (words[0] == "v")
                {
                    if (words.Length < 4) throw new FormatException("Vertex needs X Y Z.");
                    var p = new Vector3(float.Parse(words[1], CultureInfo.InvariantCulture), float.Parse(words[2], CultureInfo.InvariantCulture), float.Parse(words[3], CultureInfo.InvariantCulture)) * scale;
                    var point = PuppetPoint.From(p); point.Check("OBJ vertex");
                    if (!welded.TryGetValue(p, out var index)) { index = mesh.Vertices.Count; welded.Add(p, index); mesh.Vertices.Add(point); }
                    positions.Add(index);
                    if (positions.Count > 100000 || mesh.Vertices.Count > 32768) throw new InvalidDataException("OBJ has too many vertices.");
                }
                else if (words[0] == "f")
                {
                    if (words.Length != 4) throw new FormatException("Export a triangulated OBJ (faces must have exactly three vertices).");
                    foreach (var word in words.Skip(1))
                    {
                        var raw = int.Parse(word.Split('/')[0], CultureInfo.InvariantCulture); var index = raw < 0 ? positions.Count + raw : raw - 1;
                        if (index < 0 || index >= positions.Count) throw new FormatException("Face refers to an unknown vertex.");
                        mesh.Triangles.Add(positions[index]);
                    }
                    if (mesh.Triangles.Count > 196608) throw new InvalidDataException("OBJ has too many triangles.");
                }
            }
            catch (Exception ex) when (ex is FormatException or OverflowException) { throw new InvalidDataException($"OBJ line {lineNumber}: {ex.Message}", ex); }
        }
        new PropAsset { Id = "import-check", Name = "Import", Solids = [mesh] }.Validate();
        return mesh;
    }
}
