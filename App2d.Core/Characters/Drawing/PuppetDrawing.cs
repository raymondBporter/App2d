using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Characters;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Characters.Drawing;

/// <summary>Plain primitives over an evaluated pose; independent of source clips and anatomy names.</summary>
public sealed class PuppetDrawing
{
    public CharacterMesh Mesh { get; } = new();
    public List<CharacterDrawBatch2D> Batches { get; } = [];
    public string TextureRoot { get; private set; } = Environment.CurrentDirectory;

    /// <summary>Empties the mesh and its batches, for a drawing assembled from props alone.</summary>
    public void Clear()
    {
        Mesh.Clear();
        Batches.Clear();
    }

    public void Build(PuppetDefinition definition, PuppetPose pose) => Build(definition.Ink, definition.LineWidth, definition.Parts, pose.World, rest: id => definition.Controls.First(c => c.Id == id).Rest.XYZ);
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
            part => pose.Expressions.GetValueOrDefault(part.Id, "none"), face, id => pose.Angles[id], id => pose.Bones[id], id => model.Rest[id], id => model.RestTransforms[id]);
        for (var i = 0; i < pose.Slots.Count; i++)
        {
            var slot = pose.Slots[i];
            if (slot.Part is not { Hidden: false } part)
            {
                continue;
            }

            if (part.Material?.Texture is null)
            {
                throw new InvalidDataException("Slot attachments currently require image materials.");
            }

            var frame = PartAttachment2D.FrameOf(part, pose.World, id => pose.Angles[id], id => pose.Bones[id]);
            AddImage(part, frame, ColorRgba32.FromVector4(slot.Color).ToColor(), slot.Slot.Blend, -i * .001f);
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
        foreach (var equipment in entity.Equipment)
        {
            AddProp(equipment.Prop, placed.Socket(equipment.Socket));
        }
    }

    /// <summary>Appends a prop with its grip on the given frame. Strokes get an ink outline just behind them.</summary>
    public void AddProp(PropAsset prop, Frame3D frame)
    {
        var start = Mesh.Count;
        PropDrawing.Add(Mesh, prop, frame);
        var ink = ColorExtensions.FromHexRgb(prop.Ink);
        foreach (var shape in prop.Shapes)
        {
            var points = shape.Points.ConvertAll(p => ActorPose.PropPoint(frame, prop, p));
            var material = shape.RenderMaterial;
            var outline = material.Outline;
            var outlineColor = outline?.Color is { } color ? ColorExtensions.FromHexRgb(color) : ink;
            var outlineWidth = outline?.Width ?? prop.LineWidth;
            if (shape.IsFilled)
            {
                if (material.Fill is { } fill)
                {
                    var fillColor = ColorExtensions.FromHexRgb(fill);
                    var triangles = PolygonTriangulationCache2D.Get(shape.Points, static point => point.XY, 1e-8);
                    foreach (var (a, b, c) in MeshTopology.Triangles(triangles.Indices))
                    {
                        Mesh.Triangle(points[a], points[b], points[c], fillColor);
                    }
                }
                if (outline is not null && outlineWidth > 0)
                {
                    Mesh.Polygon(points, null, outlineColor, outlineWidth);
                }

                continue;
            }
            for (var i = 1; i < points.Count; i++)
            {
                if (outline is not null && outlineWidth > 0)
                {
                    Mesh.Line(points[i - 1] + new Vector3(0, 0, .001f), points[i] + new Vector3(0, 0, .001f), (material.StrokeWidth ?? prop.LineWidth) + outlineWidth * 2, outlineColor);
                }

                Mesh.Line(points[i - 1], points[i], material.StrokeWidth ?? prop.LineWidth,
                    material.Fill is { } fill ? ColorExtensions.FromHexRgb(fill) : ink);
            }
        }
        if (Mesh.Count > start)
        {
            Batches.Add(new(start, Mesh.Count - start));
        }
    }

    /// <summary>Plain primitives from parts and a world-position lookup. The only drawing path for both prototype and authored models.</summary>
    public void Build(string inkColor, float lineWidth, IEnumerable<PuppetPart> parts, Func<string, Vector3> world, Func<PuppetPart, string>? expression = null, FacePose? facePose = null, Func<string, float>? angle = null, Func<string, Matrix3x2>? transform = null, Func<string, Vector3>? rest = null, Func<string, Matrix3x2>? restTransform = null)
    {
        Clear();
        var ink = ColorExtensions.FromHexRgb(inkColor);
        foreach (var part in parts)
        {
            if (part.Hidden)
            {
                continue;
            }

            if (part.Material?.Texture is not null)
            {
                AddImage(part, PartAttachment2D.FrameOf(part, world, angle, transform), Microsoft.Xna.Framework.Color.White, "normal", null);
                continue;
            }
            var start = Mesh.Count;
            var material = part.RenderMaterial;
            var outline = material.Outline;
            var outlineWidth = outline?.Width ?? lineWidth;
            var outlineColor = outline?.Color is { } color ? ColorExtensions.FromHexRgb(color) : ink;
            var face = expression?.Invoke(part) ?? part.Face;
            if (part.Geometry is ICurve2D)
            {
                var path = PartContours2D.Contour(part, world, angle, transform, rest, restTransform);
                var strokeColor = material.Fill is { } fill ? ColorExtensions.FromHexRgb(fill) : ink;
                for (var i = 1; i < path.Count; i++)
                {
                    if (outline is not null && outlineWidth > 0)
                    {
                        Mesh.Line(path[i - 1], path[i], (material.StrokeWidth ?? lineWidth) + outlineWidth * 2, outlineColor);
                    }

                    Mesh.Line(path[i - 1], path[i], material.StrokeWidth ?? lineWidth, strokeColor);
                }
                if (Mesh.Count > start)
                {
                    Batches.Add(new(start, Mesh.Count - start));
                }

                continue;
            }
            var contour = PartContours2D.Contour(part, world, angle, transform, rest, restTransform);
            var triangles = part.Geometry switch
            {
                SimplePolygon2D => PolygonTriangulationCache2D.Get(PartContours2D.CutoutPoints(part), 1e-8),
                HeightFieldShape2D field => field.Polygon.Mesh,
                _ => null
            };
            var frame = PartAttachment2D.FrameOf(part, world, angle, transform);
            if (material.Fill is { } shapeFill)
            {
                var fillColor = ColorExtensions.FromHexRgb(shapeFill);
                if (triangles is not null)
                {
                    foreach (var (a, b, c) in MeshTopology.Triangles(triangles.Indices))
                    {
                        Mesh.Triangle(contour[a], contour[b], contour[c], fillColor);
                    }
                }
                else
                {
                    Mesh.Polygon(contour, fillColor, null, 0);
                }
            }
            PartPainting.Add(Mesh, part, frame, contour, triangles);
            if (outline is not null && outlineWidth > 0)
            {
                Mesh.Polygon(contour, null, outlineColor, outlineWidth);
            }

            if (face != "none")
            {
                var bounds = ShapeBounds2D.Calculate((IShape2D)part.Geometry!);
                FaceDrawing.Build(Mesh, facePose ?? FaceExpressions.Get(face),
                p => frame.At(bounds.Center + new Vector2(p.X * bounds.Width, -p.Y * bounds.Height))
                    + new Vector3(frame.Coordinates.XAxis * (part.FaceX / MathF.Max(frame.Coordinates.XAxis.Length(), 1e-10f)), -.002f), lineWidth * .6f, ink);
            }
            if (Mesh.Count > start)
            {
                Batches.Add(new(start, Mesh.Count - start));
            }
        }
    }

    private void AddImage(PuppetPart part, PartFrame2D frame, Microsoft.Xna.Framework.Color color, string blend, float? depth)
    {
        var material = part.RenderMaterial;
        var own = ColorExtensions.FromHexRgba(material.Tint ?? "ffffffff");
        var fill = material.Fill is null ? Microsoft.Xna.Framework.Color.White : ColorExtensions.FromHexRgb(material.Fill);
        var bounds = ShapeBounds2D.Calculate((IShape2D)part.Geometry!);
        color = new((byte)(color.R * own.R / 255 * fill.R / 255), (byte)(color.G * own.G / 255 * fill.G / 255),
            (byte)(color.B * own.B / 255 * fill.B / 255), (byte)(color.A * own.A / 255));
        Vector3 At(float x, float y)
        {
            var p = frame.At(bounds.Center + new Vector2(x * bounds.Width, y * bounds.Height));
            if (depth is { } z)
            {
                p.Z = z;
            }

            return p;
        }
        var a = At(-.5f, -.5f);
        var b = At(.5f, -.5f);
        var c = At(.5f, .5f);
        var d = At(-.5f, .5f);
        var start = Mesh.Count;
        Mesh.Vertex(a, color, new(0, 1));
        Mesh.Vertex(b, color, new(1, 1));
        Mesh.Vertex(c, color, new(1, 0));
        Mesh.Vertex(a, color, new(0, 1));
        Mesh.Vertex(c, color, new(1, 0));
        Mesh.Vertex(d, color, new(0, 0));
        Batches.Add(new(start, 6, material.Texture, blend, WriteDepth: depth is null));
    }
}

public sealed record CharacterDrawBatch2D(int Start, int Count, string? Texture = null, string Blend = "normal", bool WriteDepth = true);
