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
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Characters.Drawing;

/// <summary>Plain primitives over an evaluated pose; independent of source clips and anatomy names.</summary>
public sealed class PuppetDrawing
{
    public LayeredMesh Mesh { get; } = new();
    public List<CharacterDrawBatch2D> Batches { get; } = [];
    public string TextureRoot { get; private set; } = Environment.CurrentDirectory;

    /// <summary>Empties the mesh and its batches, for a drawing assembled from props alone.</summary>
    public void Clear()
    {
        Mesh.Clear();
        Batches.Clear();
    }

    /// <summary>A standalone part in its own units, with its ordinary animation clock.</summary>
    public void Build(Part asset, double seconds = 0, string? textureRoot = null)
    {
        Clear();
        TextureRoot = textureRoot ?? asset.TextureRoot;
        AddAttachment(asset, Frame3D.Identity, seconds: seconds);
    }

    /// <summary>
    /// The model's parts over the evaluated pose, then its slot attachments and library part instances through the same
    /// per-part path (P5b): any payload kind, one placement. A face is an animated instance like any other (P7c).
    /// </summary>
    public void Build(ResolvedModel model, EvaluatedPose pose)
    {
        Clear();
        TextureRoot = model.TextureRoot;
        var managed = model.Base.Skins.SelectMany(s => s.Attachments.Values).SelectMany(a => a.Values).ToHashSet(StringComparer.Ordinal);
        // The pose's part channels (P7a): a show track hides a part for its span, a fit track multiplies its leaf scale.
        Add(model.Parts.Where(p => !managed.Contains(p.Id) && pose.Visible(p)).Select(pose.Drawn), pose.World,
            pose.Placement, id => model.Rest[id], model.RestPlacement);
        for (var i = 0; i < pose.Slots.Count; i++)
        {
            var slot = pose.Slots[i];
            if (slot.Part is not { Hidden: false } part)
            {
                continue;
            }

            // A slot's attachment is an ordinary part with the slot's color and blend: an image keeps the slot order as its
            // depth, geometry draws at its own layer through the shared path.
            Add([pose.Drawn(part)], pose.World, pose.Placement, id => model.Rest[id], model.RestPlacement,
                ColorExtensions.FromVector4(slot.Color), slot.Slot.Blend, -i * .001f);
        }
        AddAttachments(model, pose);
    }

    /// <summary>
    /// Every visible library part instance on its socket (P5b): the instance placement on the socket's evaluated placement
    /// carries its locations. Every geometry entry uses the shared drawing path; meshes lift that entry's planar frame into physical 3D.
    /// </summary>
    public void AddAttachments(ResolvedModel model, EvaluatedPose pose)
    {
        if (model.Attachments.Count == 0)
        {
            return;
        }

        var actor = new ActorPose(pose, Vector2.Zero, 1);
        foreach (var attachment in model.Attachments)
        {
            if (attachment.Instance.Hidden)
            {
                continue;
            }

            // An animated instance was evaluated with the pose (P7a): its pieces draw from its nested pose.
            if (pose.Attachments.TryGetValue(attachment.Id, out var evaluated))
            {
                AddAttachment(evaluated);
                continue;
            }

            AddAttachment(attachment, actor.Socket(attachment.Socket), pose.SampleSeconds, actor.SocketPlacement(attachment.Socket).Layer);
        }
    }

    /// <summary>An evaluated animated instance: its visible, fitted pieces over its nested pose carried by its placement, including meshes.</summary>
    public void AddAttachment(EvaluatedAttachment evaluated)
    {
        if (evaluated.Hidden)
        {
            return;
        }

        var attachment = evaluated.Attachment;
        Add(evaluated.DrawnParts(), evaluated.World, evaluated.Frame, evaluated.Rest, evaluated.RestFrame);
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
            AddAttachment(equipment.Prop, placed.Socket(equipment.Socket), seconds: local.SampleSeconds, layer: placed.SocketPlacement(equipment.Socket).Layer);
        }
        Vector3 World(string id) => id == PartLocation.Origin ? Vector3.Zero : local.World(id);
        Vector3 Rest(string id) => id == PartLocation.Origin ? Vector3.Zero : entity.Model.Rest[id];
        LayeredFrame2D Bone(string id) => id is PartLocation.Origin or CharacterModel.Locomotion ? LayeredFrame2D.Identity : local.Placement(id);
        LayeredFrame2D RestBone(string id) => id is PartLocation.Origin or CharacterModel.Locomotion ? LayeredFrame2D.Identity : entity.Model.RestPlacement(id);
        foreach (var geometry in entity.Geometry.Where(g => g.Definition.Material is not null && !g.Definition.Hidden))
        {
            if (geometry.Binding is ColliderBindings.Actor or ColliderBindings.Control)
            {
                Add([geometry.Definition.ResolveMaterial(entity.Asset.Materials)], World, Bone, Rest, RestBone, seconds: local.SampleSeconds);
                continue;
            }
            var frame = geometry.Frame(placed);
            var drawing = geometry.Definition.ResolveMaterial(entity.Asset.Materials) with
            {
                A = PartLocation.Origin,
                B = null,
                Frame = PartLocation.Origin,
                Socket = null,
                Prop = null,
                OffsetX = 0,
                OffsetY = 0,
                Angle = 0,
                ScaleX = 1,
                ScaleY = 1,
                Layer = 0,
                Along = 0,
                Stretch = false,
            };
            Add([drawing], _ => frame.Origin, _ => frame, seconds: local.SampleSeconds);
        }
    }

    /// <summary>Shared library instance at a spatial socket, including weapon tilt/twist and physical Z.</summary>
    public void AddAttachment(Part asset, Frame3D socket, PartInstance? instance = null, double seconds = 0, float layer = 0)
    {
        instance ??= new() { Id = asset.Id, Asset = asset.Id, Socket = "preview" };
        var attachment = ResolvedAttachment.Resolve(instance, _ => asset,
            new Dictionary<string, ModelSocket> { [instance.Socket] = new() { Id = instance.Socket } }, "drawing");
        AddAttachment(attachment, socket, seconds, layer);
    }

    /// <summary>Places an asset on a pose's socket, carrying its drawing layer separately from its spatial frame.</summary>
    public void AddAttachment(Part asset, ActorPose pose, ModelSocket socket, PartInstance? instance = null, double seconds = 0) =>
        AddAttachment(asset, pose.Socket(socket), instance, seconds, pose.SocketPlacement(socket).Layer);

    public void AddAttachment(ResolvedAttachment attachment, ActorPose pose, double seconds = 0) =>
        AddAttachment(attachment, pose.Socket(attachment.Socket), seconds, pose.SocketPlacement(attachment.Socket).Layer);

    public void AddAttachment(ResolvedAttachment attachment, Frame3D socket, double seconds = 0, float layer = 0) =>
        AddPayload(attachment, PartPlacement3D.Placement(attachment, socket), seconds, layer + attachment.Instance.Layer);

    private void AddPayload(ResolvedAttachment attachment, Frame3D spatial, double seconds, float layer = 0)
    {
        if (attachment.Instance.Hidden) return;
        if (attachment.IsAnimated)
        {
            var evaluated = PoseEvaluator.Preview(attachment, LayeredFrame2D.Identity, seconds);
            if (!evaluated.Hidden)
            {
                Add(evaluated.DrawnParts(), evaluated.World,
                    evaluated.Frame, evaluated.Rest, evaluated.RestFrame, spatial: spatial, layer: layer);
            }

            return;
        }
        var (world, frame) = attachment.Lookups(LayeredFrame2D.Identity);
        Add(attachment.Parts, world, frame, world, frame, spatial: spatial, physicalDepth: id => attachment.Asset.Point(id)?.Z ?? 0, seconds: seconds, layer: layer);
    }

    /// <summary>Plain primitives from parts and a world-position lookup, replacing the drawing. The only drawing path for both prototype and authored models.</summary>
    public void Build(IEnumerable<PartGeometry> parts, Func<string, Vector3> world, Func<string, LayeredFrame2D>? bone = null, Func<string, Vector3>? rest = null, Func<string, LayeredFrame2D>? restBone = null)
    {
        Clear();
        Add(parts, world, bone, rest, restBone);
    }

    /// <summary>
    /// Appends parts over a world-position lookup. <paramref name="tint"/> multiplies every fill and stroke (a slot's color);
    /// <paramref name="blend"/> and <paramref name="imageDepth"/> apply to image parts, which a slot orders by depth.
    /// </summary>
    public void Add(IEnumerable<PartGeometry> parts, Func<string, Vector3> world,
        Func<string, LayeredFrame2D>? bone = null, Func<string, Vector3>? rest = null, Func<string, LayeredFrame2D>? restBone = null, Color4b? tint = null, string blend = "normal", float? imageDepth = null, Frame3D? spatial = null, Func<string, float>? physicalDepth = null, double seconds = 0, float layer = 0)
    {
        foreach (var source in parts)
        {
            if (source.Hidden || source.Material is null)
            {
                continue;
            }
            var material = source.Material.Sample(seconds, Vector4.One);
            Mesh.Layer = layer;
            var part = ReferenceEquals(material, source.Material) ? source : source with { Material = material };
            var colorMultiplier = material.AnimatedTint * new Vector4(1, 1, 1, material.Alpha ?? 1);
            var surfaceTint = (tint ?? Color4b.White).Multiply(ColorExtensions.FromHexRgba(material.Tint ?? "ffffffff")).Multiply(ColorExtensions.FromVector4(colorMultiplier));
            if (material.Texture is not null)
            {
                AddImage(part, PartAttachment2D.FrameOf(part, world, bone, rest), surfaceTint, blend, imageDepth, spatial, physicalDepth?.Invoke(part.A) ?? 0);
                continue;
            }
            var start = Mesh.Count;
            if (part.Mesh is { } mesh)
            {
                var lifted = PartPlacement3D.Extend(PartAttachment2D.FrameOf(part, world, bone, rest));
                if (spatial is { } meshFrame) lifted = PartPlacement3D.GeometryFrame(PartAttachment2D.FrameOf(part, world, bone, rest), meshFrame, physicalDepth?.Invoke(part.A) ?? 0);
                Mesh.Layer += PartAttachment2D.FrameOf(part, world, bone, rest).Layer;
                Mesh.Solid(mesh.ToIndexedMesh(), lifted, material.Fill is { } solidFill ? ColorExtensions.FromHexRgb(solidFill) : null,
                    material.Outline?.Color is { } solidInk ? ColorExtensions.FromHexRgb(solidInk) : null, material.Outline?.Width ?? 0, DirectionalLight3D.Props, surfaceTint);
                if (Mesh.Count > start) Batches.Add(new(start, Mesh.Count - start));
                continue;
            }
            var outline = material.Outline;
            var outlineWidth = outline?.Width ?? 0;
            var outlineColor = outline?.Color is { } color ? ColorExtensions.FromHexRgb(color).Multiply(surfaceTint) : (Color4b?)null;
            if (part.Geometry is ICurve2D || part.SpatialCurve is not null)
            {
                var path = PartContours2D.Contour(part, world, bone, rest, restBone);
                if (spatial is { } curveFrame) path = path.TransformedBy(curveFrame, physicalDepth?.Invoke(part.A) ?? 0);
                var strokeColor = material.Fill is { } fill ? ColorExtensions.FromHexRgb(fill).Multiply(surfaceTint) : (Color4b?)null;
                var strokeWidth = material.StrokeWidth ?? 0;
                if (part.Geometry is RoundedPolylineCurve2D)
                {
                    // One ribbon with shared cross-sections: no seams at the fillets and fewer triangles than per-segment lines.
                    if (outlineColor is { } edgeColor && outlineWidth > 0)
                    {
                        Mesh.Ribbon(path, edgeColor, strokeWidth + outlineWidth * 2);
                    }

                    if (strokeColor is { } centerColor && strokeWidth > 0) Mesh.Ribbon(path, centerColor, strokeWidth);
                    if (Mesh.Count > start)
                    {
                        Batches.Add(new(start, Mesh.Count - start));
                    }

                    continue;
                }
                for (var i = 1; i < path.Count; i++)
                {
                    if (outlineColor is { } edgeColor && outlineWidth > 0)
                    {
                        Mesh.Line(path[i - 1], path[i], strokeWidth + outlineWidth * 2, edgeColor, path.LayerAt(i - 1), path.LayerAt(i));
                    }

                    if (strokeColor is { } centerColor && strokeWidth > 0)
                        Mesh.Line(path[i - 1], path[i], strokeWidth, centerColor, path.LayerAt(i - 1), path.LayerAt(i));
                }
                if (Mesh.Count > start)
                {
                    Batches.Add(new(start, Mesh.Count - start));
                }

                continue;
            }
            var contour = PartContours2D.Contour(part, world, bone, rest, restBone);
            var localContour = contour;
            if (spatial is { } shapeFrame) contour = contour.TransformedBy(shapeFrame, physicalDepth?.Invoke(part.A) ?? 0);
            var triangles = part.Geometry switch
            {
                SimplePolygon2D => PolygonTriangulationCache2D.Get(PartContours2D.CutoutPoints(part), 1e-8),
                HeightFieldShape2D field => field.Polygon.Mesh,
                _ => null
            };
            var frame = PartAttachment2D.FrameOf(part, world, bone, rest);
            if (material.Fill is { } shapeFill)
            {
                var fillColor = ColorExtensions.FromHexRgb(shapeFill).Multiply(surfaceTint);
                if (triangles is not null)
                {
                    foreach (var (a, b, c) in MeshTopology.Triangles(triangles.Indices))
                    {
                        Mesh.Triangle(contour[a], contour[b], contour[c], fillColor, contour.LayerAt(a), contour.LayerAt(b), contour.LayerAt(c));
                    }
                }
                else
                {
                    Mesh.Polygon(contour, fillColor, null, 0);
                }
            }
            PartPainting.Add(Mesh, part, frame, localContour, triangles, spatial, physicalDepth?.Invoke(part.A) ?? 0, surfaceTint);
            if (outlineColor is { } shapeEdge && outlineWidth > 0)
            {
                Mesh.Polygon(contour, null, shapeEdge, outlineWidth);
            }

            if (Mesh.Count > start)
            {
                Batches.Add(new(start, Mesh.Count - start));
            }
        }
    }

    private void AddImage(PartGeometry part, LayeredFrame2D frame, Color4b color, string blend, float? depth, Frame3D? spatial = null, float physicalZ = 0)
    {
        var material = part.RenderMaterial;
        var fill = material.Fill is null ? Color4b.White : ColorExtensions.FromHexRgb(material.Fill);
        var bounds = ShapeBounds2D.Calculate((IShape2D)part.Geometry!);
        color = color.Multiply(fill);
        Vector3 At(float x, float y)
        {
            var p = new Vector3(frame.Frame.TransformPoint(bounds.Center + new Vector2(x * bounds.Width, y * bounds.Height)), physicalZ);
            return spatial is { } placed ? placed.TransformPoint(p) : p;
        }
        var a = At(-.5f, -.5f);
        var b = At(.5f, -.5f);
        var c = At(.5f, .5f);
        var d = At(-.5f, .5f);
        var start = Mesh.Count;
        var uv = material.Source ?? AxisAlignedRectangle2D.UnitRect;
        Vector2 Uv(float x, float y) => uv.Min + new Vector2(x, y) * uv.Size;
        var layer = depth ?? frame.Layer;
        Mesh.Vertex(a, color, Uv(0, 1), layer);
        Mesh.Vertex(b, color, Uv(1, 1), layer);
        Mesh.Vertex(c, color, Uv(1, 0), layer);
        Mesh.Vertex(a, color, Uv(0, 1), layer);
        Mesh.Vertex(c, color, Uv(1, 0), layer);
        Mesh.Vertex(d, color, Uv(0, 0), layer);
        Batches.Add(new(start, 6, material.Texture, blend, WriteDepth: depth is null));
    }
}

public sealed record CharacterDrawBatch2D(int Start, int Count, string? Texture = null, string Blend = "normal", bool WriteDepth = true);
