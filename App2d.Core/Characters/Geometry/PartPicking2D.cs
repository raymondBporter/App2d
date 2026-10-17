using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Geometry.Primitives;
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
    public static float Distance(PuppetPart part, Func<string, Vector3> world, Vector3 point, Func<string, float>? angle = null, Func<string, Matrix3x2>? transform = null, Func<string, Vector3>? rest = null, Func<string, Matrix3x2>? restTransform = null, float lineWidth = .045f)
    {
        var p = new Vector2(point.X, point.Y);
        if (part.Geometry is ICurve2D curve)
        {
            var pickWidth = MathF.Max(part.Material?.StrokeWidth ?? lineWidth, .06f);
            if (part.Bindings is { Count: > 0 } || part.WaveAmplitude != 0 || part.ScaleX != part.ScaleY)
            {
                var path = PartContours2D.Contour(part, world, angle, transform, rest, restTransform);
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

            var coordinates = PartAttachment2D.FrameOf(part, world).Coordinates;
            if (!coordinates.TryToLocal(p, out var local))
            {
                return float.PositiveInfinity;
            }

            var distance = curve.Distance(local) * coordinates.YAxis.Length();
            return distance / pickWidth;
        }
        if (part.Geometry is SimplePolygon2D && part.Bindings is { Count: > 0 })
        {
            var points = PartContours2D.Contour(part, world, angle, transform, rest, restTransform);
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
            var typedFrame = PartAttachment2D.FrameOf(part, world, angle, transform);
            var shape = typed;
            if (!typedFrame.Coordinates.TryToLocal(p, out var local))
            {
                return float.PositiveInfinity;
            }

            return shape.ContainsPoint(local) ? .5f : 1 + ShapeDistance2D.Distance(local, shape);
        }
        throw new InvalidDataException($"Part '{part.Id}' needs typed geometry.");
    }

}
