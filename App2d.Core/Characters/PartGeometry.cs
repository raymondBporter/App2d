using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>
/// Where a drawing part lies, from a world-position lookup. Drawing, picking and outlines share this one layout, so what the
/// author clicks is exactly what is drawn. Shapes attach to A; B, when set, points their local +Y axis.
/// </summary>
public static class PartGeometry
{
    public readonly record struct Frame(Vector3 Origin, Vector2 Right, Vector2 Up, float Depth)
    {
        public Vector3 At(Vector2 local) => Origin + new Vector3(Right * local.X + Up * local.Y, Depth);
    }

    public static Frame FrameOf(PuppetPart part, Func<string, Vector3> world)
    {
        var a = world(part.A); var b = part.B is { } end ? world(end) : a + Vector3.UnitY;
        var direction = new Vector2(b.X - a.X, b.Y - a.Y);
        var up = direction.LengthSquared() > 1e-10f ? Vector2.Normalize(direction) : Vector2.UnitY;
        return new(a + new Vector3(new Vector2(up.Y, -up.X) * part.OffsetX + up * part.OffsetY, 0), new(up.Y, -up.X), up, part.Depth);
    }

    /// <summary>The closed outline of an ellipse or rounded box, or a stroke's two endpoints.</summary>
    public static List<Vector3> Contour(PuppetPart part, Func<string, Vector3> world)
    {
        if (part.Kind == "stroke")
        {
            var depth = new Vector3(0, 0, part.Depth);
            return [world(part.A) + depth, world(part.B!) + depth];
        }
        var frame = FrameOf(part, world);
        var halfSize = new Vector2(part.Width / 2, part.Height / 2);
        Span<Vector2> vertices = stackalloc Vector2[part.Kind == "ellipse" ? 48 : 36];
        if (part.Kind == "ellipse")
        {
            VertexGenerator2D.WriteEllipse(vertices, Vector2.Zero, halfSize);
        }
        else
        {
            var radius = Math.Min(part.Width, part.Height) * .5f * part.Roundness;
            VertexGenerator2D.WriteRoundedRectangle(vertices, -halfSize, halfSize, radius);
        }
        var contour = new List<Vector3>(vertices.Length);
        foreach (var vertex in vertices) contour.Add(frame.At(vertex));
        return contour;
    }

    /// <summary>
    /// Normalized XY picking score, not a distance in world units. Ellipses use radial distance;
    /// rounded boxes retain their bounding-box score; strokes use width with a minimum picking tolerance.
    /// </summary>
    public static float Distance(PuppetPart part, Func<string, Vector3> world, Vector3 point)
    {
        var p = new Vector2(point.X, point.Y);
        if (part.Kind == "stroke")
        {
            var a = world(part.A); var b = world(part.B!);
            return PrimitiveGeometry2D.DistanceToSegment(p, new(a.X, a.Y), new(b.X, b.Y), 1e-10f)
                / MathF.Max(part.Width, .06f);
        }
        var frame = FrameOf(part, world); var local = p - new Vector2(frame.Origin.X, frame.Origin.Y);
        var coordinates = new Vector2(Vector2.Dot(local, frame.Right), Vector2.Dot(local, frame.Up));
        var halfSize = new Vector2(part.Width / 2, part.Height / 2);
        return part.Kind == "ellipse"
            ? PrimitiveGeometry2D.NormalizedEllipseRadius(coordinates, Vector2.Zero, halfSize)
            : PrimitiveGeometry2D.NormalizedRectangleRadius(coordinates, Vector2.Zero, halfSize);
    }
}
