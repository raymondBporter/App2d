using App2d.Core.Geometry.Builders;
using App2d.Core.Curves;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Authoring;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>Normalized character-part picking scores using the same attachment and cutout outlines as drawing.</summary>
public static class PartPicking2D
{
    public static float Distance(PuppetPart part, Func<string, Vector3> world, Vector3 point, Func<string, Vector3> rest) => Distance(part, world, point, rest: rest, angle: null);

    /// <summary>
    /// Normalized XY picking score, not a distance in world units. Curves use their local-space distance
    /// scaled into the attachment frame and retain a minimum picking width.
    /// </summary>
    public static float Distance(PuppetPart part, Func<string, Vector3> world, Vector3 point, Func<string, float>? angle = null, Func<string, Matrix3x2>? transform = null, Func<string, Vector3>? rest = null, Func<string, Matrix3x2>? restTransform = null)
    {
        var p = new Vector2(point.X, point.Y);
        if (part.Geometry is CurveDefinition2D curve)
        {
            var a = world(part.A); var b = world(part.B!);
            var origin = new Vector2(a.X, a.Y);
            var along = new Vector2(b.X - a.X, b.Y - a.Y);
            var lengthSquared = along.LengthSquared();
            if (lengthSquared < 1e-10f) return Vector2.Distance(p, origin) / MathF.Max(part.Width, .06f);
            var across = new Vector2(along.Y, -along.X);
            var delta = p - origin;
            var local = new Vector2(Vector2.Dot(delta, across), Vector2.Dot(delta, along)) / lengthSquared;
            var distance = GeometryDefinitionCache2D.CurveOf(curve).Distance(local) * MathF.Sqrt(lengthSquared);
            return distance / MathF.Max(part.Width, .06f);
        }
        if (part.Geometry is SimplePolygon2D.Builder && part.Bindings is { Count: > 0 })
        {
            var points = PartContours2D.Contour(part, world, angle, transform, rest, restTransform);
            // Folded outlines may overlap. Pick the same rest-topology triangles that are filled, not an even/odd hole.
            if (part.Bindings is { Count: > 0 })
            {
                var mesh = PolygonTriangulation2D.Triangulate(PartContours2D.CutoutPoints(part).Select(v => v.XY), 1e-8);
                float Cross(Vector2 a, Vector2 b, Vector2 q) => (b.X - a.X) * (q.Y - a.Y) - (b.Y - a.Y) * (q.X - a.X);
                for (var i = 0; i < mesh.Indices.Length; i += 3)
                {
                    var a = new Vector2(points[mesh.Indices[i]].X, points[mesh.Indices[i]].Y);
                    var b = new Vector2(points[mesh.Indices[i + 1]].X, points[mesh.Indices[i + 1]].Y);
                    var c = new Vector2(points[mesh.Indices[i + 2]].X, points[mesh.Indices[i + 2]].Y);
                    if (MathF.Abs(Cross(a, b, c)) < 1e-8f) continue;
                    var ab = Cross(a, b, p); var bc = Cross(b, c, p); var ca = Cross(c, a, p);
                    if (ab >= -1e-7f && bc >= -1e-7f && ca >= -1e-7f || ab <= 1e-7f && bc <= 1e-7f && ca <= 1e-7f) return .5f;
                }
            }
            var inside = false; var nearest = float.MaxValue;
            for (var i = 0; i < points.Count; i++)
            {
                var a = new Vector2(points[i].X, points[i].Y); var b = new Vector2(points[(i + 1) % points.Count].X, points[(i + 1) % points.Count].Y);
                nearest = MathF.Min(nearest, Distance2D.DistanceToSegment(p, a, b, 1e-10f));
                if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
            }
            return nearest < 1e-6f ? 1 : inside && part.Bindings is not { Count: > 0 } ? .5f : 1 + nearest / MathF.Max(part.Width, part.Height);
        }
        if (part.Geometry is ShapeBuilder2D typed)
        {
            var typedFrame = PartAttachment2D.FrameOf(part, world, angle, transform);
            var shape = GeometryDefinitionCache2D.ShapeOf(typed);
            var scale = PartAttachment2D.ShapeScale(part, shape);
            if (!typedFrame.Coordinates.TryToLocal(p, out var local)) return float.PositiveInfinity;
            var normalized = local / scale;
            return shape.ContainsPoint(normalized) ? .5f : 1 + ShapeDistance2D.Distance(normalized, shape);
        }
        throw new InvalidDataException($"Part '{part.Id}' needs typed geometry.");
    }

}
