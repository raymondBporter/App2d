using System.Globalization;
using System.Numerics;

namespace App2d.Core.Characters.Authored;

/// <summary>Small solid-art helpers and a deliberately limited, triangulated OBJ interchange path. No runtime file dependencies.</summary>
public static class PropGeometry
{
    /// <summary>A tapered diamond-section blade. The ridge supplies broad-face highlights without painted detail.</summary>
    public static PropSolid Blade(float root, float shoulder, float tip, float halfWidth, float halfDepth, string fill)
    {
        var mesh = new PropSolid { Fill = fill };
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
        var area = points.Select((p, i) => p.X * points[(i + 1) % points.Count].Y - p.Y * points[(i + 1) % points.Count].X).Sum();
        if (area < 0) points.Reverse();
        var n = points.Count; var mesh = new PropSolid { Fill = fill, Outline = [.. points], Thickness = thickness };
        mesh.Vertices.AddRange(points.Select(p => p with { Z = p.Z - thickness / 2 }));
        mesh.Vertices.AddRange(points.Select(p => p with { Z = p.Z + thickness / 2 }));
        foreach (var (a, b, c) in Triangulate(points))
            mesh.Triangles.AddRange([a, c, b, n + a, n + b, n + c]);
        for (var i = 0; i < n; i++) { var j = (i + 1) % n; mesh.Triangles.AddRange([i, j, n + j, i, n + j, n + i]); }
        return mesh;
    }

    // Ear clipping happens at authoring time. Runtime props already contain indexed triangles.
    private static IEnumerable<(int A, int B, int C)> Triangulate(List<PuppetPoint> points)
    {
        static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        var remaining = Enumerable.Range(0, points.Count).ToList();
        while (remaining.Count > 3)
        {
            var found = false;
            for (var i = 0; i < remaining.Count; i++)
            {
                var a = remaining[(i + remaining.Count - 1) % remaining.Count];
                var b = remaining[i]; var c = remaining[(i + 1) % remaining.Count];
                var pa = points[a].XY; var pb = points[b].XY; var pc = points[c].XY;
                if (Cross(pa, pb, pc) <= 1e-8f) continue;
                if (remaining.Any(j => j != a && j != b && j != c &&
                    Cross(pa, pb, points[j].XY) >= -1e-8f && Cross(pb, pc, points[j].XY) >= -1e-8f && Cross(pc, pa, points[j].XY) >= -1e-8f)) continue;
                yield return (a, b, c); remaining.RemoveAt(i); found = true; break;
            }
            if (!found) throw new InvalidDataException("The prop outline must be simple and have nonzero area.");
        }
        if (Cross(points[remaining[0]].XY, points[remaining[1]].XY, points[remaining[2]].XY) <= 1e-8f)
            throw new InvalidDataException("The prop outline has a degenerate final triangle.");
        yield return (remaining[0], remaining[1], remaining[2]);
    }

    /// <summary>Turns the existing convex weapon silhouettes into simple solids, preserving their color and dimensions.</summary>
    public static PropAsset Solidify(PropAsset prop, float thickness, bool centerDepth = false)
    {
        foreach (var shape in prop.Shapes)
        {
            var points = shape.Points.Select(p => centerDepth ? p with { Z = 0 } : p).ToList();
            if (shape.Kind == "polygon") prop.Solids.Add(Extrude(points, thickness, shape.Fill));
            else for (var i = 1; i < points.Count; i++)
            {
                var a = points[i - 1]; var b = points[i]; var d = Vector2.Normalize(b.XY - a.XY);
                var off = new Vector3(-d.Y, d.X, 0) * shape.Width / 2;
                prop.Solids.Add(Extrude([PuppetPoint.From(a.XYZ - off), PuppetPoint.From(b.XYZ - off), PuppetPoint.From(b.XYZ + off), PuppetPoint.From(a.XYZ + off)], shape.Width * .85f, shape.Fill));
            }
        }
        prop.Shapes.Clear(); prop.LineWidth = .012f; prop.Validate(); return prop;
    }

    /// <summary>Loads positions and triangle faces, welding coincident vertices for clean outlines. UVs/normals/materials are ignored.
    /// The caller chooses scale, grip and color. Export with +X along the weapon and +Y across its broad face.</summary>
    public static PropSolid ImportObj(string text, float scale = 1, string fill = "#c8b18a")
    {
        if (!float.IsFinite(scale) || scale <= 0) throw new InvalidDataException("OBJ scale must be positive.");
        var positions = new List<int>(); var welded = new Dictionary<Vector3, int>(); var mesh = new PropSolid { Fill = fill };
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
