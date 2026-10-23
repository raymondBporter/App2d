using App2d.Core.Characters.Authored;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>Character part outlines, including curves and cutouts deformed by control bindings.</summary>
public static class PartContours2D
{
    public static IReadOnlyList<Vector2> CutoutPoints(PuppetPart part) =>
        ((SimplePolygon2D)part.Geometry!).VertexList;

    public static List<Vector3> Contour(PuppetPart part, Func<string, Vector3> world, Func<string, Vector3> rest) => Contour(part, world, bone: null, rest: rest);

    /// <summary>A part's outline in an evaluated pose of a resolved model.</summary>
    public static List<Vector3> Contour(PuppetPart part, ResolvedModel model, EvaluatedPose pose) =>
        Contour(part, pose.World, pose.Placement, id => model.Rest[id], model.RestPlacement);

    /// <summary>A shape's closed outline or a curve's sampled open stroke.</summary>
    public static List<Vector3> Contour(PuppetPart part, Func<string, Vector3> world, Func<string, LayeredFrame2D>? bone = null, Func<string, Vector3>? rest = null, Func<string, LayeredFrame2D>? restBone = null)
    {
        if (part.Geometry is ICurve2D)
        {
            return PartCurves2D.Contour(part, world, rest);
        }

        var frame = PartAttachment2D.FrameOf(part, world, bone);
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
                // Preserve the part's depth relative to the bound control, including animated control depth.
                cutout[binding.Point] = PartAttachment2D.BoundPoint(binding, drawnRest, world, rest);
            }
            return cutout;
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

            return result;
        }
        throw new InvalidDataException($"Part '{part.Id}' needs typed geometry.");
    }

}
