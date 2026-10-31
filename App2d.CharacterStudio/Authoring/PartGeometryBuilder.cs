using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using System.Globalization;
using System.Numerics;

namespace App2d.CharacterStudio.Authoring;

/// <summary>Small solid-art helpers and a deliberately limited, triangulated OBJ interchange path. No runtime file dependencies.</summary>
public static class PartGeometryBuilder
{
    public static PartGeometry Entry(PartMesh mesh, string fill) => new()
    {
        A = PartLocation.Origin,
        Frame = PartLocation.Origin,
        Mesh = mesh,
        Material = new() { Fill = fill, Outline = new() }
    };

    /// <summary>Assign stable entry IDs when constructing a new asset from geometry helpers.</summary>
    public static Part Named(Part part)
    {
        int meshes = 0, shapes = 0;
        foreach (var entry in part.Geometry) entry.Id = entry.Mesh is not null ? $"mesh-{++meshes}" : $"shape-{++shapes}";
        return part;
    }

    /// <summary>A tapered diamond-section blade. The ridge supplies broad-face highlights without painted detail.</summary>
    public static PartGeometry Blade(float root, float shoulder, float tip, float halfWidth, float halfDepth, string fill)
    {
        var mesh = new PartMesh();
        foreach (var (x, width) in new[] { (root, halfWidth), (shoulder, halfWidth * .72f) })
        {
            mesh.Vertices.AddRange([new(x, -width, 0), new(x, 0, -halfDepth), new(x, width, 0), new(x, 0, halfDepth)]);
        }

        mesh.Vertices.Add(new(tip, 0, 0));
        mesh.Triangles.AddRange([0, 2, 1, 0, 3, 2]);
        for (var i = 0; i < 4; i++)
        {
            var j = (i + 1) % 4;
            mesh.Triangles.AddRange([i, j, 4 + j, i, 4 + j, 4 + i, 4 + i, 4 + j, 8]);
        }
        return Entry(mesh, fill);
    }

    /// <summary>Extrudes a simple XY outline, including concave silhouettes; either winding is accepted. Thickness is centered about Z.</summary>
    public static PartGeometry Extrude(IEnumerable<PuppetPoint> outline, float thickness, string fill)
    {
        var mesh = PartMesh.Extrude(outline, thickness);
        return Entry(mesh, fill);
    }

    /// <summary>Loads positions and triangle faces, welding coincident vertices for clean outlines. UVs/normals/materials are ignored.
    /// The caller chooses scale, grip and color. Export with +X along the weapon and +Y across its broad face.</summary>
    public static PartGeometry ImportObj(string text, float scale = 1, string fill = "#c8b18a")
    {
        if (!float.IsFinite(scale) || scale <= 0)
        {
            throw new InvalidDataException("OBJ scale must be positive.");
        }

        var positions = new List<int>();
        var welded = new Dictionary<Vector3, int>();
        var mesh = new PartMesh();
        var lineNumber = 0;
        foreach (var line in text.Split('\n'))
        {
            lineNumber++;
            var words = line.Split('#')[0].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
            {
                continue;
            }

            try
            {
                if (words[0] == "v")
                {
                    if (words.Length < 4)
                    {
                        throw new FormatException("Vertex needs X Y Z.");
                    }

                    var p = new Vector3(float.Parse(words[1], CultureInfo.InvariantCulture), float.Parse(words[2], CultureInfo.InvariantCulture), float.Parse(words[3], CultureInfo.InvariantCulture)) * scale;
                    var point = PuppetPoint.From(p);
                    point.Check("OBJ vertex");
                    if (!welded.TryGetValue(p, out var index))
                    {
                        index = mesh.Vertices.Count;
                        welded.Add(p, index);
                        mesh.Vertices.Add(point);
                    }
                    positions.Add(index);
                    if (positions.Count > 100000 || mesh.Vertices.Count > 32768)
                    {
                        throw new InvalidDataException("OBJ has too many vertices.");
                    }
                }
                else if (words[0] == "f")
                {
                    if (words.Length != 4)
                    {
                        throw new FormatException("Export a triangulated OBJ (faces must have exactly three vertices).");
                    }

                    foreach (var word in words.Skip(1))
                    {
                        var raw = int.Parse(word.Split('/')[0], CultureInfo.InvariantCulture);
                        var index = raw < 0 ? positions.Count + raw : raw - 1;
                        if (index < 0 || index >= positions.Count)
                        {
                            throw new FormatException("Face refers to an unknown vertex.");
                        }

                        mesh.Triangles.Add(positions[index]);
                    }
                    if (mesh.Triangles.Count > 196608)
                    {
                        throw new InvalidDataException("OBJ has too many triangles.");
                    }
                }
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                throw new InvalidDataException($"OBJ line {lineNumber}: {ex.Message}", ex);
            }
        }
        mesh.Validate();
        return Entry(mesh, fill);
    }
}
