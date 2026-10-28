using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>Normalized character-part picking scores using the same attachment and cutout outlines as drawing.</summary>
public static class PartPicking2D
{
    public static float Distance(PartGeometry part, Func<string, Vector3> world, Vector3 point, Func<string, Vector3> rest) => Distance(part, world, point, bone: null, rest: rest);

    /// <summary>The picking score of a part in an evaluated pose of a resolved model, with the pose's animated artwork fit applied.</summary>
    public static float Distance(PartGeometry part, ResolvedModel model, EvaluatedPose pose, Vector3 point) =>
        Distance(pose.Fitted(part), pose.World, point, pose.Placement, id => model.Rest[id], model.RestPlacement, model.Base.LineWidth);

    /// <summary>The picking score of an evaluated animated instance (P7a): the best of its visible pieces in their nested pose.</summary>
    public static float Distance(EvaluatedAttachment attachment, Vector3 point)
    {
        if (attachment.Hidden)
        {
            return float.PositiveInfinity;
        }

        var best = float.PositiveInfinity;
        foreach (var part in attachment.DrawnParts())
        {
            best = MathF.Min(best, Distance(part, attachment.World, point, attachment.Frame, attachment.Rest, attachment.RestFrame, attachment.Attachment.Asset.LineWidth));
        }

        return best;
    }

    /// <summary>
    /// The picking score of a library part instance at a placement (P5b): the best of its payload pieces through the same
    /// geometry picking. Shapes, curves, images and indexed meshes share this route.
    /// </summary>
    public static float Distance(ResolvedAttachment attachment, LayeredFrame2D placement, Vector3 point) =>
        DistanceAt(attachment, PartPlacement3D.Local(attachment.Asset).Then(PartPlacement3D.Extend(placement)), point);

    /// <summary>The picking score of an instance in an evaluated pose: its placement on the socket's evaluated placement, or its evaluated nested pose when animated.</summary>
    public static float Distance(ResolvedAttachment attachment, EvaluatedPose pose, Vector3 point) =>
        pose.Attachments.TryGetValue(attachment.Id, out var evaluated) ? Distance(evaluated, point)
        : Distance(attachment, new ActorPose(pose, Vector2.Zero, 1).Socket(attachment.Socket), point);

    public static float Distance(ResolvedAttachment attachment, Frame3D socket, Vector3 point) =>
        DistanceAt(attachment, PartPlacement3D.Placement(attachment, socket), point);

    private static float DistanceAt(ResolvedAttachment attachment, Frame3D spatial, Vector3 point)
    {
        if (attachment.Instance.Hidden) return float.PositiveInfinity;
        var (world, frame) = attachment.Lookups(LayeredFrame2D.Identity);
        return attachment.Parts.Select(part => Distance(part, world, point, frame, world, frame, attachment.Asset.LineWidth,
            spatial, attachment.Asset.Point(part.A)?.Z ?? 0)).DefaultIfEmpty(float.PositiveInfinity).Min();
    }

    /// <summary>
    /// Normalized XY picking score, not a distance in world units. Curves use their local-space distance
    /// scaled into the attachment frame and retain a minimum picking width.
    /// </summary>
    public static float Distance(PartGeometry part, Func<string, Vector3> world, Vector3 point, Func<string, LayeredFrame2D>? bone = null, Func<string, Vector3>? rest = null, Func<string, LayeredFrame2D>? restBone = null, float lineWidth = .045f, Frame3D? spatial = null, float physicalZ = 0)
    {
        var p = new Vector2(point.X, point.Y);
        if (part.Hidden) return float.PositiveInfinity;
        if (part.Mesh is { } solid)
        {
            var frame = PartPlacement3D.Extend(PartAttachment2D.FrameOf(part, world, bone));
            if (spatial is { } placed) frame = PartPlacement3D.GeometryFrame(PartAttachment2D.FrameOf(part, world, bone), placed, physicalZ);
            var vertices = solid.Vertices.Select(v => frame.TransformPoint(v.XYZ).XY).ToArray();
            var nearest = float.PositiveInfinity;
            foreach (var (ia, ib, ic) in MeshTopology.Triangles(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(solid.Triangles)))
            {
                var a = vertices[ia]; var b = vertices[ib]; var c = vertices[ic];
                if (MathF.Abs(a.Orientation(b, c)) > 1e-8f && new Triangle<Vector2>(a, b, c).Contains(p, 1e-7)) return 0;
                nearest = MathF.Min(nearest, MathF.Min(Distance2D.DistanceToSegment(p, a, b),
                    MathF.Min(Distance2D.DistanceToSegment(p, b, c), Distance2D.DistanceToSegment(p, c, a))));
            }
            return nearest / .06f;
        }

        if (spatial is { } projection)
        {
            var contour = PartPlacement3D.Contour(part, PartContours2D.Contour(part, world, bone, rest, restBone), projection, physicalZ);
            var closed = part.Geometry is IShape2D;
            if (closed)
            {
                var triangles = PolygonTriangulationCache2D.Get(PartContours2D.LocalContour(part), 1e-8);
                foreach (var (a, b, c) in MeshTopology.Triangles(triangles.Indices))
                {
                    if (MathF.Abs(contour[a].XY.Orientation(contour[b].XY, contour[c].XY)) > 1e-8f &&
                        new Triangle<Vector2>(contour[a].XY, contour[b].XY, contour[c].XY).Contains(p, 1e-7))
                    {
                        return 0;
                    }
                }
            }
            var nearest = float.PositiveInfinity;
            foreach (var (a, b) in MeshTopology.Edges(contour.Count, closed))
                nearest = MathF.Min(nearest, Distance2D.DistanceToSegment(p, contour[a].XY, contour[b].XY));
            return nearest / MathF.Max(part.Material?.StrokeWidth ?? lineWidth, .06f);
        }

        if (part.Geometry is ICurve2D curve)
        {
            var pickWidth = MathF.Max(part.Material?.StrokeWidth ?? lineWidth, .06f);
            if (part.Frame is not null || part.Bindings is { Count: > 0 } || part.ScaleX != part.ScaleY)
            {
                var path = PartContours2D.Contour(part, world, bone, rest, restBone);
                var nearest = float.PositiveInfinity;
                foreach (var (start, end) in MeshTopology.Edges(path.Count, closed: false))
                {
                    nearest = MathF.Min(nearest, Distance2D.DistanceToSegment(p, path[start].XY, path[end].XY));
                }

                return nearest / pickWidth;
            }
            var a = world(part.A);
            var b = world(part.B!);
            var along = b.XY - a.XY;
            var lengthSquared = along.LengthSquared();
            if (lengthSquared < 1e-10f)
            {
                return Vector2.Distance(p, a.XY) / pickWidth;
            }

            var coordinates = PartAttachment2D.FrameOf(part, world).Frame;
            if (!coordinates.TryToLocal(p, out var local))
            {
                return float.PositiveInfinity;
            }

            var distance = curve.Distance(local) * coordinates.YAxis.Length();
            return distance / pickWidth;
        }
        if (part.Geometry is SimplePolygon2D && part.Bindings is { Count: > 0 })
        {
            var points = PartContours2D.Contour(part, world, bone, rest, restBone);
            // Folded outlines may overlap. Pick the same rest-topology triangles that are filled, not an even/odd hole.
            if (part.Bindings is { Count: > 0 })
            {
                var mesh = PolygonTriangulationCache2D.Get(PartContours2D.CutoutPoints(part), 1e-8);
                foreach (var (ia, ib, ic) in MeshTopology.Triangles(mesh.Indices))
                {
                    var a = points[ia].XY;
                    var b = points[ib].XY;
                    var c = points[ic].XY;
                    if (MathF.Abs(a.Orientation(b, c)) < 1e-8f)
                    {
                        continue;
                    }

                    if (new Triangle<Vector2>(a, b, c).Contains(p, 1e-7))
                    {
                        return .5f;
                    }
                }
            }
            var nearest = float.MaxValue;
            foreach (var (ia, ib) in MeshTopology.Edges(points.Count))
            {
                var a = points[ia].XY;
                var b = points[ib].XY;
                nearest = MathF.Min(nearest, Distance2D.DistanceToSegment(p, a, b, 1e-10f));
            }
            return nearest < 1e-6f ? 1 : 1 + nearest;
        }
        if (part.Geometry is IShape2D typed)
        {
            var typedFrame = PartAttachment2D.FrameOf(part, world, bone);
            var shape = typed;
            if (!typedFrame.TryToLocal(p, out var local))
            {
                return float.PositiveInfinity;
            }

            return shape.ContainsPoint(local) ? .5f : 1 + ShapeDistance2D.Distance(local, shape);
        }
        throw new InvalidDataException($"Part '{part.Id}' needs typed geometry.");
    }

}
