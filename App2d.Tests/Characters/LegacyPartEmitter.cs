using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Shapes;
using System.Text;

namespace App2d.Tests.Characters;

/// <summary>
/// Writes authored worn and held <see cref="Part"/>s out as C# source: one function per part that returns its pieces as
/// attachments on a mount. A part's pivot is taken out of its geometry, because an attachment is already in its mount's axes.
/// Solids with the same outline are written once and shared, so five characters' copies of one beard become one mesh. Dies
/// with the authored parts.
/// </summary>
internal static class LegacyPartEmitter
{
    private const string Ink = "#222b32";

    private static string Number(float value) => LegacyClipEmitter.Number(value);
    private static string Point(Vector3f point) => $"new({Number(point.X)}, {Number(point.Y)}, {Number(point.Z)})";

    public static string Emit(string className, string summary, string regenerateWith, IReadOnlyList<Part> parts)
    {
        var meshes = new Dictionary<string, string>(StringComparer.Ordinal);
        var fields = new StringBuilder();
        var functions = new StringBuilder();
        var names = new List<(string Id, string Function)>();

        // A solid's mesh, written once however many parts use it.
        string Mesh(string owner, string expression)
        {
            if (meshes.TryGetValue(expression, out var existing)) return existing;
            meshes.Add(expression, owner);
            fields.Append($"    private static readonly IndexedMesh3D {owner} = {expression};\n");
            return owner;
        }

        foreach (var part in parts)
        {
            Assert.True(part.Scale == 1 && !part.IsAnimated, $"{part.Id}: scaled or animated parts are not written.");
            var pivot = part.Point(part.Pivot) ?? throw new InvalidDataException($"{part.Id}: unknown pivot.");
            var function = LegacyClipEmitter.Pascal(part.Id);
            names.Add((part.Id, function));
            var pieces = new List<string>();
            foreach (var piece in part.Geometry)
            {
                Assert.True(piece is { A: PartLocation.Origin, B: null, Frame: PartLocation.Origin, ScaleX: 1, ScaleY: 1, Angle: 0, OffsetX: 0, OffsetY: 0, Along: 0, Hidden: false },
                    $"{part.Id} {piece.Id}: a piece placed anywhere but the part's own origin is not written.");
                var material = piece.RenderMaterial;
                Assert.True(material is { Texture: null, Paint: null, Fill: not null }, $"{part.Id} {piece.Id}: only plain fills are written.");
                var edge = material.Outline is { Color: not null, Width: > 0 } outline ? outline : null;
                var paint = $"Paint(\"{material.Fill}\"{(edge is null ? "" : ", " + Number(edge.Width!.Value))}"
                    + (edge is null || edge.Color == Ink ? "" : $", \"{edge.Color}\"")
                    + (material.StrokeWidth is { } stroke ? $", stroke: {Number(stroke)}" : "") + ")";
                var name = $"\"{part.Id}/{piece.Id}\"";
                var layer = piece.Layer == 0 ? "layer" : piece.Layer < 0 ? $"layer - {Number(-piece.Layer)}" : $"layer + {Number(piece.Layer)}";
                var owner = function + LegacyClipEmitter.Pascal(piece.Id);
                if (piece.Mesh is { } mesh)
                {
                    // An outline is the readable form; a mesh authored as raw triangles is written as it is.
                    var expression = mesh.Outline is { } rim
                        ? LegacyClipEmitter.Wrap("", $"Extrude({Number(mesh.Thickness)}, ", [.. rim.Select(v => Point(v - pivot))], 4, "").TrimStart()
                        : LegacyClipEmitter.Wrap("", "new([", [.. mesh.Vertices.Select(v => Point(v - pivot))], 4, "", $"], [{string.Join(", ", mesh.Triangles)}])").TrimStart();
                    pieces.Add($"new MountedSolid({name}, mount, {Mesh(owner, expression)}, {paint}, {layer})");
                    continue;
                }
                Assert.True(pivot == Vector3f.Zero || piece.Geometry is LineSegmentCurve2D, $"{part.Id} {piece.Id}: a flat shape on a part with a pivot is not written.");
                switch (piece.Geometry)
                {
                    case LineSegmentCurve2D line:
                        Assert.True(piece.VertexZ is null || piece.VertexZ.All(z => z == 0), $"{part.Id} {piece.Id}: a line with depth is not written.");
                        pieces.Add($"new MountedPath({name}, mount, [{LegacyClipEmitter.Vector(line.Start - pivot.XY)}, {LegacyClipEmitter.Vector(line.End - pivot.XY)}], {paint}, {layer})");
                        break;
                    case Ellipse2D ellipse:
                        Assert.Equal(0, ellipse.AngleRadians, 5);
                        pieces.Add($"new MountedShape({name}, mount, Ellipse2D.Builder.Create({LegacyClipEmitter.Vector(ellipse.Radii)}, {LegacyClipEmitter.Vector(ellipse.Center)}), {paint}, {layer})");
                        break;
                    case Rectangle2D rectangle:
                        Assert.Equal(0, rectangle.AngleRadians, 5);
                        pieces.Add($"new MountedShape({name}, mount, Rectangle2D.Builder.FromSize({Number(rectangle.Width)}, {Number(rectangle.Height)}, {LegacyClipEmitter.Vector(rectangle.Center)}), {paint}, {layer})");
                        break;
                    default:
                        Assert.Fail($"{part.Id} {piece.Id}: this kind of geometry is not written.");
                        break;
                }
            }
            functions.Append($"\n    /// <summary>{part.Name}.</summary>\n    public static ImmutableArray<Attachment> {function}(int mount, float layer = 0) =>\n    [\n");
            foreach (var piece in pieces) functions.Append("        ").Append(piece).Append(",\n");
            functions.Append("    ];\n");
            // Named points on the thing, from where it is held: a blade's tip, a gun's muzzle. They are used in the mount's plane,
            // so any depth is left out.
            foreach (var location in part.Locations.Where(l => l.Id != part.Pivot && (l.Point - pivot).XY != Vector2f.Zero))
            {
                functions.Append($"    public static readonly Vector2f {function}{LegacyClipEmitter.Pascal(location.Id)} = {LegacyClipEmitter.Vector((location.Point - pivot).XY)};\n");
            }
        }

        var source = new StringBuilder();
        source.Append("// Generated from the authored JSON parts by LegacyPartEmitter in App2d.Tests. While those parts exist, change them and\n");
        source.Append($"// regenerate (set APP2D_WRITE_CLIPS=1 and run the tests named {regenerateWith}) instead of editing this file.\n");
        source.Append("using App2d.Core.Geometry.Meshes;\nusing App2d.Core.Geometry.Shapes;\nusing System.Collections.Immutable;\n\nnamespace App2d.Core.Characters;\n\n");
        source.Append($"/// <summary>{summary}</summary>\npublic static class {className}\n{{\n");
        source.Append("    /// <summary>A plain fill, edged in ink unless told otherwise; a stroke width makes it a line.</summary>\n");
        source.Append($"    private static RenderMaterialDefinition2D Paint(string fill, float edge = 0, string edgeColor = \"{Ink}\", float? stroke = null) =>\n");
        source.Append("        new() { Fill = fill, StrokeWidth = stroke, Outline = edge > 0 ? new() { Color = edgeColor, Width = edge } : null };\n\n");
        source.Append("    /// <summary>A flat outline pushed out to a thickness.</summary>\n");
        source.Append("    private static IndexedMesh3D Extrude(float thickness, params ReadOnlySpan<Vector3f> outline) =>\n");
        source.Append("        MeshGenerators3D.Extrude(MeshGenerators3D.CounterClockwise(outline.ToArray()), thickness);\n\n");
        source.Append(fields).Append(functions);
        source.Append("\n    /// <summary>Every thing by the name its authored part had.</summary>\n");
        source.Append("    public static IReadOnlyDictionary<string, Func<int, float, ImmutableArray<Attachment>>> ByName { get; } =\n");
        source.Append("        new Dictionary<string, Func<int, float, ImmutableArray<Attachment>>>(StringComparer.Ordinal)\n        {\n");
        foreach (var (id, function) in names) source.Append($"            [\"{id}\"] = {function},\n");
        source.Append("        };\n}\n");
        return source.ToString();
    }
}
