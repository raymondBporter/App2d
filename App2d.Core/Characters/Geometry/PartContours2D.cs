using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Curves;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>Character part outlines, including curves and cutouts deformed by control bindings.</summary>
public static class PartContours2D
{
    public static IReadOnlyList<Vector2> CutoutPoints(PartGeometry part) =>
        ((SimplePolygon2D)part.Geometry!).VertexList;

    public static PartContour Contour(PartGeometry part, Func<string, Vector3> world, Func<string, Vector3> rest) => Contour(part, world, bone: null, rest: rest);

    /// <summary>A part's outline in an evaluated pose of a resolved model, with the pose's animated artwork fit applied (P7a).</summary>
    public static PartContour Contour(PartGeometry part, ResolvedModel model, EvaluatedPose pose) =>
        Contour(pose.Fitted(part), pose.World, pose.Placement, id => model.Rest[id], model.RestPlacement);

    /// <summary>A piece's outline inside an evaluated animated instance, in the wearer's frame.</summary>
    public static PartContour Contour(PartGeometry part, EvaluatedAttachment attachment) =>
        Contour(attachment.Pose.Fitted(part), attachment.World, attachment.Frame, attachment.Rest, attachment.RestFrame);

    public static PartContour Contour(PartGeometry part, ResolvedAttachment attachment, Frame3D socket)
    {
        var (world, frame) = attachment.Lookups(LayeredFrame2D.Identity);
        var spatial = PartPlacement3D.Placement(attachment, socket);
        var physicalZ = attachment.Asset.Point(part.A)?.Z ?? 0;
        if (part.Mesh is { } mesh)
        {
            var placed = PartPlacement3D.GeometryFrame(PartAttachment2D.FrameOf(part, world, frame), spatial, physicalZ);
            return new([.. (mesh.Outline ?? mesh.Vertices).Select(p => placed.TransformPoint(p.ToVector3()))], PartAttachment2D.FrameOf(part, world, frame).Layer);
        }
        return Contour(part, world, frame, world, frame).TransformedBy(spatial, physicalZ);
    }

    /// <summary>A shape's closed outline or a curve's sampled open stroke.</summary>
    public static PartContour Contour(PartGeometry part, Func<string, Vector3> world, Func<string, LayeredFrame2D>? bone = null, Func<string, Vector3>? rest = null, Func<string, LayeredFrame2D>? restBone = null)
    {
        if (part.SpatialCurve is { } spatialCurve)
        {
            var placement = PartAttachment2D.FrameOf(part, world, bone, rest);
            var curve = spatialCurve.TransformedBy(PartPlacement3D.Extend(placement));
            return new([.. Enumerable.Range(0, part.CurveSections + 1).Select(i => curve.Evaluate(i / (float)part.CurveSections).ToVector3())], placement.Layer);
        }
        if (part.Geometry is ICurve2D && part.Frame is null)
        {
            return PartCurves2D.Contour(part, world, rest);
        }

        var frame = PartAttachment2D.FrameOf(part, world, bone, rest);
        if (part.Mesh is { } mesh)
        {
            var lifted = PartPlacement3D.Extend(frame);
            return new([.. (mesh.Outline ?? mesh.Vertices).Select(v => lifted.TransformPoint(v.ToVector3()))], frame.Layer);
        }
        if (part.Geometry is ICurve2D || part.VertexZ is not null)
        {
            return new([.. LocalContour(part).Select((v, i) => new Vector3(frame.Frame.TransformPoint(v), part.VertexZ?[i] ?? 0))], frame.Layer);
        }
        if (part.Geometry is SimplePolygon2D && part.Bindings is { Count: > 0 })
        {
            var cutout = CutoutPoints(part).Select(frame.At).ToList();
            if (rest is null)
            {
                throw new InvalidOperationException("A bound cutout needs the model's rest pose.");
            }

            var artRest = PartAttachment2D.FrameOf(part, rest, restBone);
            foreach (var binding in part.Bindings)
            {
                var point = CutoutPoints(part)[binding.Point];
                var drawnRest = artRest.At(point);
                // Preserve the part's layer relative to the bound control, including animated layer changes.
                cutout[binding.Point] = PartAttachment2D.BoundPoint(binding, drawnRest, world, rest);
            }
            return new([.. cutout.Select(p => new Vector3(p.XY, 0))], layers: [.. cutout.Select(p => p.Z)]);
        }
        if (part.Geometry is IShape2D typed)
        {
            var shape = typed;
            var count = shape.GetOutlineVertCount(48);
            if (count == 0)
            {
                throw new NotSupportedException($"A {typed.Kind} has no drawable outline.");
            }

            Span<Vector2> buffer = count <= 64 ? stackalloc Vector2[count] : new Vector2[count];
            var outline = shape.GetOutlineVerts(buffer, 48);
            var result = new List<Vector3>(count);
            foreach (var vertex in outline)
            {
                result.Add(frame.At(vertex));
            }

            return new([.. result.Select(p => new Vector3(p.XY, 0))], frame.Layer);
        }
        throw new InvalidDataException($"Part '{part.Id}' needs typed geometry.");
    }

    /// <summary>Local vertices used for rigid geometry, physical depths and explicit format conversion.</summary>
    public static List<Vector2> LocalContour(PartGeometry part)
    {
        if (part.Geometry is IShape2D shape)
        {
            var vertices = new Vector2[shape.GetOutlineVertCount(48)];
            shape.GetOutlineVerts(vertices, 48);
            return [.. vertices];
        }
        return part.Geometry switch
        {
            LineSegmentCurve2D line => [line.Start, line.End],
            PolylineCurve2D line => [.. line.Points],
            ICurve2D curve => [.. Curve2D.Sample(curve.Validated(), 32)],
            _ => throw new InvalidDataException("A contour requires a shape or curve.")
        };
    }

}
