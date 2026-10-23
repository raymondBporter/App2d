using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>Normalized character-part picking scores using the same attachment and cutout outlines as drawing.</summary>
public static class PartPicking2D
{
    public static float Distance(PuppetPart part, Func<string, Vector3> world, Vector3 point, Func<string, Vector3> rest) => Distance(part, world, point, bone: null, rest: rest);

    /// <summary>The picking score of a part in an evaluated pose of a resolved model.</summary>
    public static float Distance(PuppetPart part, ResolvedModel model, EvaluatedPose pose, Vector3 point) =>
        Distance(part, pose.World, point, pose.Placement, id => model.Rest[id], model.RestPlacement, model.Base.LineWidth);

    /// <summary>
    /// The picking score of a library part instance at a placement (P5b): the best of its payload pieces through the same
    /// part picking, and of its solids by their outline in the placement's plane. Shapes, curves, images and meshes share it.
    /// </summary>
    public static float Distance(ResolvedAttachment attachment, LayeredFrame2D placement, Vector3 point)
    {
        var (world, frame) = attachment.Lookups(placement);
        var best = float.PositiveInfinity;
        foreach (var part in attachment.Parts.Where(p => !p.Hidden))
        {
            best = MathF.Min(best, Distance(part, world, point, frame, world, frame, attachment.Asset.LineWidth));
        }

        var p = point.XY;
        foreach (var solid in attachment.Solids)
        {
            var outline = (solid.Outline ?? solid.Vertices).Select(v => placement.At(new Vector3(v.X, v.Y, 0)).XY).ToArray();
            if (outline.Length < 3)
            {
                continue;
            }

            var inside = false;
            var nearest = float.PositiveInfinity;
            for (int i = 0, j = outline.Length - 1; i < outline.Length; j = i++)
            {
                var a = outline[j];
                var b = outline[i];
                if (a.Y > p.Y != b.Y > p.Y && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
                {
                    inside = !inside;
                }

                nearest = MathF.Min(nearest, Distance2D.DistanceToSegment(p, a, b));
            }
            best = MathF.Min(best, inside ? 0 : nearest / .06f);
        }
        return best;
    }

    /// <summary>The picking score of an instance in an evaluated pose: its placement on the socket's evaluated placement.</summary>
    public static float Distance(ResolvedAttachment attachment, EvaluatedPose pose, Vector3 point) =>
        Distance(attachment, attachment.Placement(new ActorPose(pose, Vector2.Zero, 1).SocketPlacement(attachment.Socket)), point);

    /// <summary>
    /// Normalized XY picking score, not a distance in world units. Curves use their local-space distance
    /// scaled into the attachment frame and retain a minimum picking width.
    /// </summary>
    public static float Distance(PuppetPart part, Func<string, Vector3> world, Vector3 point, Func<string, LayeredFrame2D>? bone = null, Func<string, Vector3>? rest = null, Func<string, LayeredFrame2D>? restBone = null, float lineWidth = .045f)
    {
        var p = new Vector2(point.X, point.Y);
        if (part.Geometry is ICurve2D curve)
        {
            var pickWidth = MathF.Max(part.Material?.StrokeWidth ?? lineWidth, .06f);
            if (part.Bindings is { Count: > 0 } || part.Wave is { Deforms: true } || part.ScaleX != part.ScaleY)
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
