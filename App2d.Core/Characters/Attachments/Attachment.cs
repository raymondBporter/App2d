using App2d.Core.Geometry.Clipping;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Primitives;
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
public abstract record Attachment(string Name, AttachmentMaterial Material, float Layer)
{
    /// <summary>Adds this piece's triangles to the mesh for a posed skeleton.</summary>
    public abstract void Paint(LayeredMesh mesh, PosedSkeleton2D pose);

    /// <summary>
    /// This piece flipped left to right in its mount's own axes, for the other side of a pair: the right glove from the left.
    /// A piece drawn through joints has no side and returns itself.
    /// </summary>
    public virtual Attachment Mirrored() => this;

    /// <summary>
    /// This piece for a skeleton that numbers its joints and mounts differently, as a <see cref="SkeletonGraft2D"/> does, at a
    /// size; null when a joint or mount it is drawn on is gone there. A line's width is kept at any size.
    /// </summary>
    public abstract Attachment? Renumbered(ImmutableArray<int> joints, ImmutableArray<int> mounts, float scale = 1);

    /// <summary>Every piece that still has somewhere to be drawn, renumbered.</summary>
    public static ImmutableArray<Attachment> Renumbered(ImmutableArray<Attachment> pieces, ImmutableArray<int> joints, ImmutableArray<int> mounts, float scale = 1) =>
        [.. pieces.Select(p => p.Renumbered(joints, mounts, scale)).OfType<Attachment>()];

    /// <summary>A point flipped left to right.</summary>
    internal static Vector2f Flip(Vector2f point) => new(-point.X, point.Y);
}

/// <summary>A second colour painted over part of a shape and cut to its outline: a belly, a wing's bone. Its corners are in the shape's own axes and must be convex.</summary>
public sealed record PaintPatch(string Fill, ImmutableArray<Vector2f> Corners)
{
    /// <summary>How much nearer the viewer each patch sits than the one under it, so it never fights the shape for the same depth.</summary>
    internal const float Lift = .00001f;
}

/// <summary>A flat shape drawn in a mount's own axes, filled and edged. It moves and turns with the mount and never bends.</summary>
public sealed record MountedShape(string Name, int Mount, IShape2D Shape, AttachmentMaterial Material, float Layer = 0) : Attachment(Name, Material, Layer)
{
    /// <summary>How many straight pieces a round shape's outline is cut into.</summary>
    public const int RoundSegments = 48;

    /// <summary>Further colours painted over the fill, under the edge.</summary>
    public ImmutableArray<PaintPatch> Patches { get; init; } = [];

    public override Attachment? Renumbered(ImmutableArray<int> joints, ImmutableArray<int> mounts, float scale = 1) =>
        mounts[Mount] < 0 ? null : Reshaped(point => point * scale) with { Mount = mounts[Mount] };

    public override Attachment Mirrored() => Reshaped(Flip);

    /// <summary>This shape with every point moved. A round shape stays round; anything else becomes a polygon of its outline.</summary>
    private MountedShape Reshaped(Func<Vector2f, Vector2f> move)
    {
        var corners = new Vector2f[Shape.GetOutlineVertCount(RoundSegments)];
        Shape.GetOutlineVerts(corners, RoundSegments);
        // Every move made here is a flip or an even scaling, so an ellipse keeps its axes: its radii follow one unit step.
        IShape2D moved = Shape is Ellipse2D { AngleRadians: 0 } ellipse
            ? Ellipse2D.Builder.Create(ellipse.Radii * (move(Vector2f.UnitY) - move(Vector2f.Zero)).Y, move(ellipse.Center))
            : SimplePolygon2D.Builder.Create(corners.Select(move));
        return this with { Shape = moved, Patches = [.. Patches.Select(p => p with { Corners = [.. p.Corners.Select(move)] })] };
    }

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
        var corners = Shape.GetOutlineVerts(buffer, RoundSegments).ToArray();
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
        for (var i = 0; i < Patches.Length; i++)
        {
            var color = ColorExtensions.FromHexRgb(Patches[i].Fill);
            var lift = -PaintPatch.Lift * (i + 1);
            void Patch(List<Vector2f> cut) => mesh.Polygon(new LayeredContour([.. cut.Select(p => place.TransformPoint(new(p, 0)))], lift), color, null, 0);
            // Cut to each triangle of a polygon, which may be concave, or to the whole of a convex outline.
            if (Shape is SimplePolygon2D concave)
            {
                foreach (var (a, b, c) in MeshTopology.Triangles(concave.Mesh.Indices)) Patch(PolygonClipping2D.ClipConvex(Patches[i].Corners, [corners[a], corners[b], corners[c]]));
            }
            else
            {
                Patch(PolygonClipping2D.ClipConvex(Patches[i].Corners, corners));
            }
        }
        if (AttachmentPainter.Edge(Material) is { } edge) mesh.Polygon(outline, null, edge.Color, edge.Width);
    }
}

/// <summary>One corner of a <see cref="SkinnedShape"/>: a point in a mount's own axes, at a layer on top of that mount's.</summary>
public readonly record struct SkinCorner(int Mount, Vector2f Point, float Layer = 0);

/// <summary>
/// A flat polygon whose corners ride on different mounts, so it stretches as they move apart: a wing's skin between its
/// shoulder, elbow and tip. <see cref="Outline"/> is the shape as drawn at rest; it decides how the polygon is cut into
/// triangles and where its patches lie. <see cref="Corners"/> holds one entry per corner of that outline.
/// </summary>
public sealed record SkinnedShape(string Name, SimplePolygon2D Outline, ImmutableArray<SkinCorner> Corners, AttachmentMaterial Material, float Layer = 0)
    : Attachment(Name, Material, Layer)
{
    /// <summary>Further colours painted over the fill, under the edge. They stretch with the triangles they lie in.</summary>
    public ImmutableArray<PaintPatch> Patches { get; init; } = [];

    public override Attachment? Renumbered(ImmutableArray<int> joints, ImmutableArray<int> mounts, float scale = 1) => Corners.Any(c => mounts[c.Mount] < 0) ? null : this with
    {
        Outline = SimplePolygon2D.Builder.Create(Outline.VertexList.Select(v => v * scale)),
        Corners = [.. Corners.Select(c => c with { Mount = mounts[c.Mount], Point = c.Point * scale })],
        Patches = [.. Patches.Select(p => p with { Corners = [.. p.Corners.Select(c => c * scale)] })],
    };

    public override void Paint(LayeredMesh mesh, PosedSkeleton2D pose)
    {
        mesh.Layer = 0;
        var points = new Vector3f[Corners.Length];
        var layers = new float[Corners.Length];
        for (var i = 0; i < points.Length; i++)
        {
            var corner = Corners[i];
            points[i] = new(pose.Skeleton.Mounts[corner.Mount].World(pose.World).TransformPoint(corner.Point), 0);
            layers[i] = pose.MountLayer(corner.Mount) + corner.Layer + Layer;
        }
        var rest = Outline.VertexList;
        var triangles = Outline.Mesh.Indices;
        if (AttachmentPainter.Fill(Material) is { } fill)
        {
            foreach (var (a, b, c) in MeshTopology.Triangles(triangles)) mesh.Triangle(points[a], points[b], points[c], fill, layers[a], layers[b], layers[c]);
        }
        for (var i = 0; i < Patches.Length; i++)
        {
            var color = ColorExtensions.FromHexRgb(Patches[i].Fill);
            var lift = PaintPatch.Lift * (i + 1);
            foreach (var (a, b, c) in MeshTopology.Triangles(triangles))
            {
                // A patch corner keeps its place inside the resting triangle as that triangle stretches.
                var cut = PolygonClipping2D.ClipConvex(Patches[i].Corners, [rest[a], rest[b], rest[c]]);
                var placed = new Vector3f[cut.Count];
                var placedLayers = new float[cut.Count];
                for (var k = 0; k < cut.Count; k++)
                {
                    var weight = TriangleMath2D.Barycentric(rest[a], rest[b], rest[c], cut[k]);
                    placed[k] = points[a] * weight.X + points[b] * weight.Y + points[c] * weight.Z;
                    placedLayers[k] = layers[a] * weight.X + layers[b] * weight.Y + layers[c] * weight.Z - lift;
                }
                mesh.Polygon(new LayeredContour(placed, layers: placedLayers), color, null, 0);
            }
        }
        if (AttachmentPainter.Edge(Material) is { } edge) mesh.Polygon(new LayeredContour(points, layers: layers), null, edge.Color, edge.Width);
    }
}

/// <summary>
/// A line through joints, rounded at each joint in between: a limb that bends with its elbow or knee. Its points are the
/// joints themselves, so it needs no mount and always lies in the skeleton's plane. Its layer runs from each joint's to the
/// next, so a leg whose hip and foot sit at different layers passes smoothly between them.
/// </summary>
public sealed record JointPath(string Name, ImmutableArray<int> Joints, AttachmentMaterial Material, float Layer = 0, float Softness = 1, int Sections = 20)
    : Attachment(Name, Material, Layer)
{
    public override Attachment? Renumbered(ImmutableArray<int> joints, ImmutableArray<int> mounts, float scale = 1) =>
        Joints.Any(j => joints[j] < 0) ? null : this with { Joints = [.. Joints.Select(j => joints[j])] };

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
public sealed record MountedPath(string Name, int Mount, ImmutableArray<Vector2f> Points, AttachmentMaterial Material, float Layer = 0) : Attachment(Name, Material, Layer)
{
    public override Attachment Mirrored() => this with { Points = [.. Points.Select(Flip)] };

    public override Attachment? Renumbered(ImmutableArray<int> joints, ImmutableArray<int> mounts, float scale = 1) =>
        mounts[Mount] < 0 ? null : this with { Mount = mounts[Mount], Points = [.. Points.Select(p => p * scale)] };

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
public sealed record MountedSolid(string Name, int Mount, IndexedMesh3D Solid, AttachmentMaterial Material, float Layer = 0) : Attachment(Name, Material, Layer)
{
    /// <summary>A solid made by pushing a flat outline out to a thickness.</summary>
    public static MountedSolid Extruded(string name, int mount, IEnumerable<Vector3f> outline, float thickness, AttachmentMaterial material, float layer = 0) =>
        new(name, mount, MeshGenerators3D.Extrude(MeshGenerators3D.CounterClockwise(outline), thickness), material, layer);

    public override Attachment? Renumbered(ImmutableArray<int> joints, ImmutableArray<int> mounts, float scale = 1) =>
        mounts[Mount] < 0 ? null : this with { Mount = mounts[Mount], Solid = scale == 1 ? Solid : new([.. Solid.Vertices.Select(v => v * scale)], Solid.Triangles) };

    /// <summary>Flipping a solid turns every face inside out, so each triangle is wound the other way to face outward again.</summary>
    public override Attachment Mirrored()
    {
        var indices = Solid.Triangles.ToArray();
        for (var i = 0; i + 2 < indices.Length; i += 3) (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
        return this with { Solid = new([.. Solid.Vertices.Select(v => new Vector3f(-v.X, v.Y, v.Z))], indices) };
    }

    public override void Paint(LayeredMesh mesh, PosedSkeleton2D pose)
    {
        mesh.Layer = pose.MountLayer(Mount) + Layer;
        var edge = AttachmentPainter.Edge(Material);
        mesh.Solid(Solid, pose.MountFrame(Mount), AttachmentPainter.Fill(Material), edge?.Color, edge?.Width ?? 0, DirectionalLight3D.Props);
    }
}
