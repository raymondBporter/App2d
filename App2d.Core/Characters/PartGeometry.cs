using App2d.Core.Meshes;
using App2d.Core.Geometry;
using App2d.Core.Shapes;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>
/// Where a drawing part lies, from a world-position lookup. Drawing, picking and outlines share this one layout, so what the
/// author clicks is exactly what is drawn. Shapes attach to A; B points their local +Y axis, or Frame uses a bone's angle.
/// </summary>
public static class PartGeometry
{
    public readonly record struct Frame(Vector3 Origin, Vector2 Right, Vector2 Up, float Depth)
    {
        public Vector3 At(Vector2 local) => Origin + new Vector3(Right * local.X + Up * local.Y, Depth);
    }

    public static void CheckCutout(IReadOnlyList<PuppetPoint>? points)
    {
        if (points is not { Count: >= 3 and <= 64 }) throw new InvalidDataException("A cutout needs 3 to 64 perimeter points.");
        foreach (var point in points)
        {
            point.Check("cutout.point");
            if (point.Z != 0) throw new InvalidDataException("Cutout coordinates are XY only.");
        }
        try { TriangleMesh2D.TriangulateSimplePolygon(points.Select(p => p.XY), 1e-8); }
        catch (ArgumentException ex) { throw new InvalidDataException("Cutout must be a simple polygon with area: " + ex.Message, ex); }
    }

    public static Frame FrameOf(PuppetPart part, Func<string, Vector3> world, Func<string, float>? angle = null)
    {
        if (part.Frame is { } bone)
        {
            var baseAngle = angle is null ? throw new InvalidOperationException("A bone-attached part needs bone angles.") : angle(bone);
            var origin = world(part.A);
            var position = Rotation2D.Apply(new(part.OffsetX, part.OffsetY), baseAngle);
            var right = Rotation2D.Apply(Vector2.UnitX, baseAngle + part.Angle);
            var boneUp = Rotation2D.Apply(Vector2.UnitY, baseAngle + part.Angle);
            return new(origin + new Vector3(position, 0), right, boneUp, part.Depth);
        }
        var a = world(part.A); var b = part.B is { } end ? world(end) : a + Vector3.UnitY;
        var direction = new Vector2(b.X - a.X, b.Y - a.Y);
        var up = direction.LengthSquared() > 1e-10f ? Vector2.Normalize(direction) : Vector2.UnitY;
        return new(a + new Vector3(new Vector2(up.Y, -up.X) * part.OffsetX + up * part.OffsetY, 0), new(up.Y, -up.X), up, part.Depth);
    }

    /// <summary>The closed outline of an ellipse, rounded box, trapezoid or cutout, or a stroke's two endpoints.</summary>
    public static List<Vector3> Contour(PuppetPart part, Func<string, Vector3> world, Func<string, float>? angle = null)
    {
        if (PuppetPartKinds.IsStroke(part.Kind))
        {
            var depth = new Vector3(0, 0, part.Depth);
            return [world(part.A) + depth, world(part.B!) + depth];
        }
        var frame = FrameOf(part, world, angle);
        if (part.Kind == "polygon")
            return part.Points!.Select(p => frame.At(new(p.X * part.Width, p.Y * part.Height))).ToList();
        var halfSize = new Vector2(part.Width / 2, part.Height / 2);
        Span<Vector2> vertices = stackalloc Vector2[part.Kind == PuppetPartKinds.Ellipse ? 48 : 36];
        if (part.Kind == PuppetPartKinds.Ellipse)
        {
            new Ellipse2D(halfSize).WriteVertices(vertices);
        }
        else
        {
            var radius = Math.Min(part.Width, part.Height) * .5f * part.Roundness;
            VertexGenerator2D.WriteRoundedRectangle(vertices, -halfSize, halfSize, radius);
            if (part.Kind == PuppetPartKinds.Trapezoid)
                for (var i = 0; i < vertices.Length; i++)
                    vertices[i].X *= TrapezoidWidthScale(part, vertices[i].Y);
        }
        var contour = new List<Vector3>(vertices.Length);
        foreach (var vertex in vertices) contour.Add(frame.At(vertex));
        return contour;
    }

    /// <summary>
    /// Normalized XY picking score, not a distance in world units. Ellipses use radial distance;
    /// rounded boxes retain their bounding-box score with taper accounted for; strokes use width with a minimum picking tolerance.
    /// </summary>
    public static float Distance(PuppetPart part, Func<string, Vector3> world, Vector3 point, Func<string, float>? angle = null)
    {
        var p = new Vector2(point.X, point.Y);
        if (PuppetPartKinds.IsStroke(part.Kind))
        {
            var a = world(part.A); var b = world(part.B!);
            return Distance2D.DistanceToSegment(p, new(a.X, a.Y), new(b.X, b.Y), 1e-10f)
                / MathF.Max(part.Width, .06f);
        }
        var frame = FrameOf(part, world, angle);
        var local = p - new Vector2(frame.Origin.X, frame.Origin.Y);
        var coordinates = new Vector2(Vector2.Dot(local, frame.Right), Vector2.Dot(local, frame.Up));
        if (part.Kind == "polygon")
        {
            var q = new Vector2(coordinates.X / part.Width, coordinates.Y / part.Height);
            var inside = false; var nearest = float.MaxValue; var points = part.Points!;
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i].XY; var b = points[(i + 1) % points.Count].XY;
                nearest = MathF.Min(nearest, Distance2D.DistanceToSegment(q, a, b, 1e-10f));
                if ((a.Y > q.Y) != (b.Y > q.Y) && q.X < (b.X - a.X) * (q.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
            }
            return nearest < 1e-6f ? 1 : inside ? .5f : 1 + nearest;
        }
        if (part.Kind == "trapezoid") coordinates.X /= TrapezoidWidthScale(part, coordinates.Y);
        var halfSize = new Vector2(part.Width / 2, part.Height / 2);
        return part.Kind == PuppetPartKinds.Ellipse
            ? Containment2D.NormalizedEllipseRadius(coordinates, Vector2.Zero, halfSize)
            : Containment2D.NormalizedRectangleRadius(coordinates, Vector2.Zero, halfSize);
    }

    private static float TrapezoidWidthScale(PuppetPart part, float y) =>
        1 + (part.TopWidthScale - 1) * Math.Clamp(y / part.Height + .5f, 0, 1);
}
