using App2d.Core.Geometry;
using App2d.Core.Geometry.Authoring;
using App2d.Core.Geometry.Builders;
using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Mathematics;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Characters.Geometry;

/// <summary>Normalized character-part picking scores using the same attachment and cutout outlines as drawing.</summary>
public static class PartPicking2D
{
    public static float Distance(PuppetPart part, Func<string, Vector3> world, Vector3 point, Func<string, Vector3> rest) => Distance(part, world, point, angle: null, rest: rest);

    /// <summary>
    /// Normalized XY picking score, not a distance in world units. Curves use their local-space distance
    /// scaled into the attachment frame and retain a minimum picking width.
    /// </summary>
    public static float Distance(PuppetPart part, Func<string, Vector3> world, Vector3 point, Func<string, float>? angle = null, Func<string, Matrix3x2>? transform = null, Func<string, Vector3>? rest = null, Func<string, Matrix3x2>? restTransform = null)
    {
        var p = new Vector2(point.X, point.Y);
        if (part.Geometry is CurveDefinition2D curve)
        {
            if (part.Bindings is { Count: > 0 } || part.WaveAmplitude != 0)
            {
                var path = PartContours2D.Contour(part, world, angle, transform, rest, restTransform);
                var nearest = float.PositiveInfinity;
                for (var i = 1; i < path.Count; i++)
                    nearest = MathF.Min(nearest, Distance2D.DistanceToSegment(p, path[i - 1].XY, path[i].XY));
                return nearest / MathF.Max(part.Width, .06f);
            }
            var a = world(part.A); var b = world(part.B!);
            var along = b.XY - a.XY;
            var lengthSquared = along.LengthSquared();
            if (lengthSquared < 1e-10f) return Vector2.Distance(p, a.XY) / MathF.Max(part.Width, .06f);
            var coordinates = Frame2D.FromYAxis(a.XY, along);
            if (!coordinates.TryToLocal(p, out var local)) return float.PositiveInfinity;
            var distance = GeometryDefinitionCache2D.CurveOf(curve).Distance(local) * MathF.Sqrt(lengthSquared);
            return distance / MathF.Max(part.Width, .06f);
        }
        if (part.Geometry is SimplePolygon2D.Builder && part.Bindings is { Count: > 0 })
        {
            var points = PartContours2D.Contour(part, world, angle, transform, rest, restTransform);
            // Folded outlines may overlap. Pick the same rest-topology triangles that are filled, not an even/odd hole.
            if (part.Bindings is { Count: > 0 })
            {
                var mesh = PolygonTriangulation2D.Triangulate(PartContours2D.CutoutPoints(part), 1e-8);
                for (var i = 0; i < mesh.Indices.Length; i += 3)
                {
                    var a = new Vector2(points[mesh.Indices[i]].X, points[mesh.Indices[i]].Y);
                    var b = new Vector2(points[mesh.Indices[i + 1]].X, points[mesh.Indices[i + 1]].Y);
                    var c = new Vector2(points[mesh.Indices[i + 2]].X, points[mesh.Indices[i + 2]].Y);
                    if (MathF.Abs(a.Orientation(b, c)) < 1e-8f) continue;
                    var ab = a.Orientation(b, p); var bc = b.Orientation(c, p); var ca = c.Orientation(a, p);
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
