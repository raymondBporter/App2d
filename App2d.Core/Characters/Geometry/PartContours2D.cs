using App2d.Core.Geometry.Authoring;
using App2d.Core.Geometry.Builders;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>Character part outlines, including curves and cutouts deformed by control bindings.</summary>
public static class PartContours2D
{
    public static IReadOnlyList<Vector2> CutoutPoints(PuppetPart part) =>
        ((SimplePolygon2D.Builder)part.Geometry!).Vertices;

    public static List<Vector3> Contour(PuppetPart part, Func<string, Vector3> world, Func<string, Vector3> rest) => Contour(part, world, angle: null, rest: rest);

    /// <summary>A shape's closed outline or a curve's sampled open stroke.</summary>
    public static List<Vector3> Contour(PuppetPart part, Func<string, Vector3> world, Func<string, float>? angle = null, Func<string, Matrix3x2>? transform = null, Func<string, Vector3>? rest = null, Func<string, Matrix3x2>? restTransform = null)
    {
        if (part.Geometry is CurveDefinition2D)
        {
            return PartCurves2D.Contour(part, world, rest);
        }

        var frame = PartAttachment2D.FrameOf(part, world, angle, transform);
        if (part.Geometry is SimplePolygon2D.Builder && part.Bindings is { Count: > 0 })
        {
            var cutout = CutoutPoints(part).Select(p => frame.At(new(p.X * part.Width, p.Y * part.Height))).ToList();
            if (rest is null)
            {
                throw new InvalidOperationException("A bound cutout needs the model's rest pose.");
            }

            var artRest = PartAttachment2D.FrameOf(part, rest, transform: restTransform);
            foreach (var binding in part.Bindings)
            {
                var point = CutoutPoints(part)[binding.Point];
                var drawnRest = artRest.At(new(point.X * part.Width, point.Y * part.Height));
                // Preserve the part's depth relative to the bound control, including animated control depth.
                cutout[binding.Point] = PartAttachment2D.BoundPoint(binding, drawnRest, world, rest);
            }
            return cutout;
        }
        if (part.Geometry is ShapeBuilder2D typed)
        {
            var shape = GeometryDefinitionCache2D.ShapeOf(typed);
            var count = shape.GetOutlineVertCount(48);
            if (count == 0)
            {
                throw new NotSupportedException($"A {typed.Kind} has no drawable outline.");
            }

            Span<Vector2> buffer = count <= 64 ? stackalloc Vector2[count] : new Vector2[count];
            var outline = shape.GetOutlineVerts(buffer, 48);
            var typedFrame = PartAttachment2D.FrameOf(part, world, angle, transform);
            var scale = PartAttachment2D.ShapeScale(part, shape);
            var result = new List<Vector3>(count);
            foreach (var vertex in outline)
            {
                result.Add(typedFrame.At(vertex * scale));
            }

            return result;
        }
        throw new InvalidDataException($"Part '{part.Id}' needs typed geometry.");
    }

}
