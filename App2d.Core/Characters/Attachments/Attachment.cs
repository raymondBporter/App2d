using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Kinematics;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Rendering;
using System.Collections.Immutable;

namespace App2d.Core.Characters;

/// <summary>
/// One drawn piece of a character: how it looks, and one number for its place in the drawing order. That number is an
/// offset from the layer of the joint or mount the piece is on, so a piece on the left arm is drawn with the left arm
/// wherever a clip puts it; smaller is nearer the viewer. A character is a skeleton and a list of these. The pose stays 2D
/// and knows nothing about them, and each kind of piece knows how to paint itself.
/// </summary>
public abstract record Attachment(string Name, RenderMaterialDefinition2D Material, float Layer)
{
    /// <summary>Adds this piece's triangles to the mesh for a posed skeleton.</summary>
    public abstract void Paint(LayeredMesh mesh, PosedSkeleton2D pose);
}

/// <summary>A flat shape drawn in a mount's own axes, filled and edged. It moves and turns with the mount and never bends.</summary>
public sealed record MountedShape(string Name, int Mount, IShape2D Shape, RenderMaterialDefinition2D Material, float Layer = 0) : Attachment(Name, Material, Layer)
{
    /// <summary>How many straight pieces a round shape's outline is cut into.</summary>
    public const int RoundSegments = 48;

    /// <summary>Adds the shape's outline in the skeleton's own space, for a mount that lies flat.</summary>
    public void Trace(Skeleton2D skeleton, ReadOnlySpan<RigidTransform2D> world, List<Vector2f> points)
    {
        var place = skeleton.Mounts[Mount].World(world).ToFrame();
        var count = Shape.GetOutlineVertCount(RoundSegments);
        Span<Vector2f> buffer = count <= 64 ? stackalloc Vector2f[count] : new Vector2f[count];
        foreach (var vertex in Shape.GetOutlineVerts(buffer, RoundSegments)) points.Add(place.TransformPoint(vertex));
    }

    public override void Paint(LayeredMesh mesh, PosedSkeleton2D pose)
    {
        mesh.Layer = pose.MountLayer(Mount) + Layer;
        var place = pose.MountFrame(Mount);
        var count = Shape.GetOutlineVertCount(RoundSegments);
        Span<Vector2f> buffer = count <= 64 ? stackalloc Vector2f[count] : new Vector2f[count];
        var points = new Vector3f[count];
        var corners = Shape.GetOutlineVerts(buffer, RoundSegments);
        for (var i = 0; i < count; i++) points[i] = place.TransformPoint(new(corners[i], 0));
        var outline = new LayeredContour(points);
        if (AttachmentPainter.Fill(Material) is { } fill)
        {
            // A polygon may be concave, so it is filled by its own triangles; any other outline is convex and fills as a fan.
            if (Shape is SimplePolygon2D polygon)
            {
                foreach (var (a, b, c) in MeshTopology.Triangles(polygon.Mesh.Indices)) mesh.Triangle(outline[a], outline[b], outline[c], fill);
            }
            else
            {
                mesh.Polygon(outline, fill, null, 0);
            }
        }
        if (AttachmentPainter.Edge(Material) is { } edge) mesh.Polygon(outline, null, edge.Color, edge.Width);
    }
}

/// <summary>
/// A line through joints, rounded at each joint in between: a limb that bends with its elbow or knee. Its points are the
/// joints themselves, so it needs no mount and always lies in the skeleton's plane. Its layer runs from each joint's to the
/// next, so a leg whose hip and foot sit at different layers passes smoothly between them.
/// </summary>
public sealed record JointPath(string Name, ImmutableArray<int> Joints, RenderMaterialDefinition2D Material, float Layer = 0, float Softness = 1, int Sections = 20)
    : Attachment(Name, Material, Layer)
{
    /// <summary>Adds the line's points in the skeleton's own space.</summary>
    public void Trace(ReadOnlySpan<RigidTransform2D> world, List<Vector2f> points)
    {
        if (Joints.Length == 2)
        {
            points.Add(world[Joints[0]].Translation);
            points.Add(world[Joints[1]].Translation);
            return;
        }
        var curve = Curve(world);
        foreach (var amount in curve.SampleParameters(Sections)) points.Add(curve.Evaluate(amount));
    }

    private RoundedPolylineCurve2D Curve(ReadOnlySpan<RigidTransform2D> world)
    {
        var corners = new Vector2f[Joints.Length];
        for (var i = 0; i < corners.Length; i++) corners[i] = world[Joints[i]].Translation;
        return RoundedPolylineCurve2D.Builder.Create(corners, Softness);
    }

    public override void Paint(LayeredMesh mesh, PosedSkeleton2D pose)
    {
        mesh.Layer = 0;
        var width = Material.StrokeWidth ?? 0;
        // A single segment is just a line.
        if (Joints.Length == 2)
        {
            AttachmentPainter.Segment(mesh, new(pose.World[Joints[0]].Translation, 0), new(pose.World[Joints[1]].Translation, 0), Material,
                pose.LayerOf(Joints[0]) + Layer, pose.LayerOf(Joints[1]) + Layer);
            return;
        }
        var curve = Curve(pose.World);
        Span<float> jointLayers = stackalloc float[Joints.Length];
        for (var i = 0; i < jointLayers.Length; i++) jointLayers[i] = pose.LayerOf(Joints[i]) + Layer;
        var amounts = curve.SampleParameters(Sections);
        var points = new Vector3f[amounts.Length];
        var layers = new float[amounts.Length];
        for (var i = 0; i < amounts.Length; i++)
        {
            points[i] = new(curve.Evaluate(amounts[i]), 0);
            layers[i] = curve.Interpolate(jointLayers, amounts[i]);
        }
        // One ribbon through a bending path leaves no seams at the corners.
        var path = new LayeredContour(points, layers: layers);
        if (AttachmentPainter.Edge(Material) is { } edge) mesh.Ribbon(path, edge.Color, width + edge.Width * 2);
        if (AttachmentPainter.Fill(Material) is { } ink && width > 0) mesh.Ribbon(path, ink, width);
    }
}

/// <summary>A line of straight segments drawn in a mount's own axes, such as the wrap on a sword's grip.</summary>
public sealed record MountedPath(string Name, int Mount, ImmutableArray<Vector2f> Points, RenderMaterialDefinition2D Material, float Layer = 0) : Attachment(Name, Material, Layer)
{
    public override void Paint(LayeredMesh mesh, PosedSkeleton2D pose)
    {
        mesh.Layer = pose.MountLayer(Mount) + Layer;
        var place = pose.MountFrame(Mount);
        for (var i = 1; i < Points.Length; i++)
        {
            AttachmentPainter.Segment(mesh, place.TransformPoint(new(Points[i - 1], 0)), place.TransformPoint(new(Points[i], 0)), Material);
        }
    }
}

/// <summary>
/// A solid drawn in a mount's own axes: the one kind of piece with real depth. Its faces are shaded by a fixed light and its
/// silhouette is edged, so a blade or a boot reads as a thing with thickness. X and Y lie in the mount's plane; Z is toward
/// or away from the viewer.
/// </summary>
public sealed record MountedSolid(string Name, int Mount, IndexedMesh3D Solid, RenderMaterialDefinition2D Material, float Layer = 0) : Attachment(Name, Material, Layer)
{
    /// <summary>A solid made by pushing a flat outline out to a thickness.</summary>
    public static MountedSolid Extruded(string name, int mount, IEnumerable<Vector3f> outline, float thickness, RenderMaterialDefinition2D material, float layer = 0) =>
        new(name, mount, MeshGenerators3D.Extrude(MeshGenerators3D.CounterClockwise(outline), thickness), material, layer);

    public override void Paint(LayeredMesh mesh, PosedSkeleton2D pose)
    {
        mesh.Layer = pose.MountLayer(Mount) + Layer;
        var edge = AttachmentPainter.Edge(Material);
        mesh.Solid(Solid, pose.MountFrame(Mount), AttachmentPainter.Fill(Material), edge?.Color, edge?.Width ?? 0, DirectionalLight3D.Props);
    }
}
