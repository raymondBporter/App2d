using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Meshes;
using App2d.Core.Curves;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Rendering.Characters;

/// <summary>Plain primitives over an evaluated pose; independent of source clips and anatomy names.</summary>
public sealed class PuppetDrawing
{
    public CharacterMesh Mesh { get; } = new();
    public List<CharacterDrawBatch2D> Batches { get; } = [];
    public string TextureRoot { get; private set; } = Environment.CurrentDirectory;
    public void Build(PuppetDefinition definition, PuppetPose pose) => Build(definition.Ink, definition.LineWidth, definition.Parts, pose.World);
    /// <summary>
    /// Faces come from the pose's evaluated expressions, never from the parts' stored defaults. <paramref name="face"/>, when
    /// given, replaces the drawn expression with a gameplay-blended face on every part that shows one; a clip that hides the
    /// face ("none", for a back view) still hides it.
    /// </summary>
    public void Build(ResolvedModel model, EvaluatedPose pose, FacePose? face = null)
    {
        TextureRoot = model.TextureRoot;
        var managed = model.Base.Skins.SelectMany(s => s.Attachments.Values).SelectMany(a => a.Values).ToHashSet(StringComparer.Ordinal);
        Build(model.Base.Ink, model.Base.LineWidth, model.Parts.Where(p => !managed.Contains(p.Id)), pose.World,
            part => pose.Expressions.GetValueOrDefault(part.Id, "none"), face, id => pose.Angles[id], id => pose.Bones[id]);
        for (var i = 0; i < pose.Slots.Count; i++)
        {
            var slot = pose.Slots[i]; if (slot.Part is not { Hidden: false } part) continue;
            if (part.Material?.Texture is null) throw new InvalidDataException("Slot attachments currently require image materials.");
            var frame = PartGeometry.FrameOf(part, pose.World, id => pose.Angles[id], id => pose.Bones[id]);
            AddImage(part, frame, SlotColorTrack2D.FormatColor(slot.Color), slot.Slot.Blend, -i * .001f);
        }
    }

    /// <summary>
    /// An entity's final pose in actor-local units, with its equipped props placed by the same socket transform that hit
    /// regions use. Facing and position belong to the caller's world matrix.
    /// </summary>
    public void Build(ResolvedEntity entity, EvaluatedPose local)
    {
        Build(entity.Model, local);
        var placed = new ActorPose(local, Vector2.Zero, 1);
        foreach (var equipment in entity.Equipment) AddProp(equipment.Prop, placed.Socket(equipment.Socket));
    }

    /// <summary>Appends a prop with its grip on the given frame. Strokes get an ink outline just behind them.</summary>
    public void AddProp(PropAsset prop, SocketFrame frame)
    {
        var start = Mesh.Count;
        PropDrawing.Add(Mesh, prop, frame);
        var ink = ColorExtensions.FromHexRgb(prop.Ink);
        foreach (var shape in prop.Shapes)
        {
            var points = shape.Points.Select(p => ActorPose.PropPoint(frame, prop, p)).ToList();
            var material = shape.RenderMaterial;
            var outline = material.Outline;
            var outlineColor = outline?.Color is { } color ? ColorExtensions.FromHexRgb(color) : ink;
            var outlineWidth = outline?.Width ?? prop.LineWidth;
            if (shape.IsFilled)
            {
                if (material.Fill is { } fill)
                {
                    var fillColor = ColorExtensions.FromHexRgb(fill);
                    var triangles = TriangleMesh2D.TriangulateSimplePolygon(shape.Points.Select(point => point.XY), 1e-8);
                    var indices = triangles.Indices;
                    for (var i = 0; i < indices.Length; i += 3)
                        Mesh.Triangle(points[indices[i]], points[indices[i + 1]], points[indices[i + 2]], fillColor);
                }
                if (outline is not null && outlineWidth > 0)
                    Mesh.Polygon(points, null, outlineColor, outlineWidth);
                continue;
            }
            for (var i = 1; i < points.Count; i++)
            {
                if (outline is not null && outlineWidth > 0)
                    Mesh.Line(points[i - 1] + new Vector3(0, 0, .001f), points[i] + new Vector3(0, 0, .001f), shape.Width + outlineWidth * 2, outlineColor);
                Mesh.Line(points[i - 1], points[i], shape.Width,
                    material.Fill is { } fill ? ColorExtensions.FromHexRgb(fill) : ink);
            }
        }
        if (Mesh.Count > start) Batches.Add(new(start, Mesh.Count - start));
    }

    /// <summary>Plain primitives from parts and a world-position lookup. The only drawing path for both prototype and authored models.</summary>
    public void Build(string inkColor, float lineWidth, IEnumerable<PuppetPart> parts, Func<string, Vector3> world, Func<PuppetPart, string>? expression = null, FacePose? facePose = null, Func<string, float>? angle = null, Func<string, Matrix3x2>? transform = null)
    {
        Mesh.Clear(); Batches.Clear(); var ink = ColorExtensions.FromHexRgb(inkColor);
        foreach (var part in parts)
        {
            if (part.Hidden) continue;
            if (part.Material?.Texture is not null)
            {
                AddImage(part, PartGeometry.FrameOf(part, world, angle, transform), "ffffffff", "normal", null);
                continue;
            }
            var start = Mesh.Count;
            var material = part.RenderMaterial;
            var outline = material.Outline;
            var outlineWidth = outline?.Width ?? lineWidth;
            var outlineColor = outline?.Color is { } color ? ColorExtensions.FromHexRgb(color) : ink;
            var face = expression?.Invoke(part) ?? part.Face;
            if (part.Geometry is CurveDefinition2D)
            {
                var path = PartGeometry.Contour(part, world);
                var strokeColor = material.Fill is { } fill ? ColorExtensions.FromHexRgb(fill) : ink;
                for (var i = 1; i < path.Count; i++)
                {
                    if (outline is not null && outlineWidth > 0)
                        Mesh.Line(path[i - 1], path[i], part.Width + outlineWidth * 2, outlineColor);
                    Mesh.Line(path[i - 1], path[i], part.Width, strokeColor);
                }
                if (Mesh.Count > start) Batches.Add(new(start, Mesh.Count - start));
                continue;
            }
            var contour = PartGeometry.Contour(part, world, angle, transform);
            var frame = PartGeometry.FrameOf(part, world, angle, transform);
            if (material.Fill is { } shapeFill)
            {
                var fillColor = ColorExtensions.FromHexRgb(shapeFill);
                if (part.Geometry is ShapeDefinition2D typed && PartGeometry.ShapeOf(typed) is SimplePolygon2D polygon)
                    Mesh.Add(polygon.Mesh, vertex => frame.At(new(vertex.X * part.Width, vertex.Y * part.Height)), fillColor);
                else Mesh.Polygon(contour, fillColor, null, 0);
            }
            PartPainting.Add(Mesh, part, frame, contour);
            if (outline is not null && outlineWidth > 0)
                Mesh.Polygon(contour, null, outlineColor, outlineWidth);
            if (face != "none")
            {
                FaceDrawing.Build(Mesh, facePose ?? FaceExpressions.Get(face),
                p => frame.At(new(p.X * part.Width + part.FaceX, -p.Y * part.Height)) - new Vector3(0, 0, .002f), lineWidth * .6f, ink);
            }
            if (Mesh.Count > start) Batches.Add(new(start, Mesh.Count - start));
        }
    }

    private void AddImage(PuppetPart part, PartGeometry.Frame frame, string tint, string blend, float? depth)
    {
        var material = part.RenderMaterial;
        static Microsoft.Xna.Framework.Color Color(string value)
        {
            var rgba = Convert.ToUInt32(value, 16);
            return new((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
        }
        var color = Color(tint); var own = Color(material.Tint ?? "ffffffff");
        var fill = material.Fill is null ? Microsoft.Xna.Framework.Color.White : ColorExtensions.FromHexRgb(material.Fill);
        color = new((byte)(color.R * own.R / 255 * fill.R / 255), (byte)(color.G * own.G / 255 * fill.G / 255),
            (byte)(color.B * own.B / 255 * fill.B / 255), (byte)(color.A * own.A / 255));
        Vector3 At(float x, float y) { var p = frame.At(new(x * part.Width, y * part.Height)); if (depth is { } z) p.Z = z; return p; }
        var a = At(-.5f, -.5f); var b = At(.5f, -.5f); var c = At(.5f, .5f); var d = At(-.5f, .5f); var start = Mesh.Count;
        Mesh.Vertex(a, color, new(0, 1)); Mesh.Vertex(b, color, new(1, 1)); Mesh.Vertex(c, color, new(1, 0));
        Mesh.Vertex(a, color, new(0, 1)); Mesh.Vertex(c, color, new(1, 0)); Mesh.Vertex(d, color, new(0, 0));
        Batches.Add(new(start, 6, material.Texture, blend, WriteDepth: depth is null));
    }
}

public sealed record CharacterDrawBatch2D(int Start, int Count, string? Texture = null, string Blend = "normal", bool WriteDepth = true);
