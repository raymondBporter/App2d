using App2d.Core.Geometry.Builders;
using App2d.Core.Curves;
using App2d.Core.Geometry.Authoring;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>Character part outlines, including curves and cutouts deformed by control bindings.</summary>
public static class PartContours2D
{
    public static IReadOnlyList<PuppetPoint> CutoutPoints(PuppetPart part) =>
        ((SimplePolygon2D.Builder)part.Geometry!).Vertices.Select(p => new PuppetPoint(p.X, p.Y)).ToArray();

    public static List<Vector3> Contour(PuppetPart part, Func<string, Vector3> world, Func<string, Vector3> rest) => Contour(part, world, rest: rest, angle: null);

    /// <summary>The closed outline of an ellipse, rounded box, trapezoid or cutout, or a stroke's two endpoints.</summary>
    public static List<Vector3> Contour(PuppetPart part, Func<string, Vector3> world, Func<string, float>? angle = null, Func<string, Matrix3x2>? transform = null, Func<string, Vector3>? rest = null, Func<string, Matrix3x2>? restTransform = null)
    {
        if (part.Geometry is CurveDefinition2D curve)
        {
            var a = world(part.A); var b = world(part.B!);
            var across = new Vector2(b.Y - a.Y, a.X - b.X);
            var along = new Vector2(b.X - a.X, b.Y - a.Y);
            var result = new List<Vector3>();
            var geometry = GeometryDefinitionCache2D.CurveOf(curve);
            var count = geometry switch { LineSegmentCurve2D => 2, PolylineCurve2D polyline => polyline.Points.Length, _ => 33 };
            for (var i = 0; i < count; i++)
            {
                var p = geometry.Evaluate((float)i / (count - 1));
                result.Add(new Vector3(new Vector2(a.X, a.Y) + across * p.X + along * p.Y, a.Z + (b.Z - a.Z) * p.Y + part.Depth));
            }
            return result;
        }
        var frame = PartAttachment2D.FrameOf(part, world, angle, transform);
        if (part.Geometry is SimplePolygon2D.Builder && part.Bindings is { Count: > 0 })
        {
            var cutout = CutoutPoints(part).Select(p => frame.At(new(p.X * part.Width, p.Y * part.Height))).ToList();
            if (rest is null) throw new InvalidOperationException("A bound cutout needs the model's rest pose.");
            var artRest = PartAttachment2D.FrameOf(part, rest, transform: restTransform);
            foreach (var binding in part.Bindings)
            {
                var point = CutoutPoints(part)[binding.Point];
                var drawnRest = artRest.At(new(point.X * part.Width, point.Y * part.Height));
                var bindRest = PartAttachment2D.BindingFrame(binding, rest); var bindNow = PartAttachment2D.BindingFrame(binding, world);
                var offset = drawnRest - bindRest.Origin;
                var local = bindRest.Coordinates.ToLocal(new(drawnRest.X, drawnRest.Y));
                // Preserve the part's depth relative to the bound control, including animated control depth.
                cutout[binding.Point] = bindNow.At(local) + new Vector3(0, 0, offset.Z);
            }
            return cutout;
        }
        if (part.Geometry is ShapeBuilder2D typed)
        {
            var shape = GeometryDefinitionCache2D.ShapeOf(typed);
            var count = shape.GetOutlineVertCount(48);
            if (count == 0) throw new NotSupportedException($"A {typed.Kind} has no drawable outline.");
            Span<Vector2> buffer = count <= 64 ? stackalloc Vector2[count] : new Vector2[count];
            var outline = shape.GetOutlineVerts(buffer, 48);
            var typedFrame = PartAttachment2D.FrameOf(part, world, angle, transform);
            var scale = PartAttachment2D.ShapeScale(part, shape);
            var result = new List<Vector3>(count);
            foreach (var vertex in outline) result.Add(typedFrame.At(vertex * scale));
            return result;
        }
        throw new InvalidDataException($"Part '{part.Id}' needs typed geometry.");
    }

}
