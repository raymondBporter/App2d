using App2d.Core.Meshes;
using App2d.Core.Geometry;
using App2d.Core.Shapes;
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

    public static Frame FrameOf(PuppetPart part, Func<string, Vector3> world)
    {
        var a = world(part.A); var b = part.B is { } end ? world(end) : a + Vector3.UnitY;
        var direction = new Vector2(b.X - a.X, b.Y - a.Y);
        var up = direction.LengthSquared() > 1e-10f ? Vector2.Normalize(direction) : Vector2.UnitY;
        return new(a + new Vector3(new Vector2(up.Y, -up.X) * part.OffsetX + up * part.OffsetY, 0), new(up.Y, -up.X), up, part.Depth);
    }

    /// <summary>The closed outline of an ellipse, rounded box, trapezoid or cutout, or a stroke's two endpoints.</summary>
    public static List<Vector3> Contour(PuppetPart part, Func<string, Vector3> world, Func<string, Vector3>? rest = null)
    {
        if (PuppetPartKinds.IsStroke(part.Kind))
        {
            var depth = new Vector3(0, 0, part.Depth);
            return [world(part.A) + depth, world(part.B!) + depth];
        }
        var frame = FrameOf(part, world);
        if (part.Kind == PuppetPartKinds.Polygon)
        {
            var cutout = part.Points!.Select(p => frame.At(new(p.X * part.Width, p.Y * part.Height))).ToList();
            if (part.Bindings is not { Count: > 0 }) return cutout;
            if (rest is null) throw new InvalidOperationException("A bound cutout needs the model's rest pose.");
            var artRest = FrameOf(part, rest);
            foreach (var binding in part.Bindings)
            {
                var point = part.Points![binding.Point];
                var drawnRest = artRest.At(new(point.X * part.Width, point.Y * part.Height));
                var bindRest = BindingFrame(binding, rest); var bindNow = BindingFrame(binding, world);
                var offset = drawnRest - bindRest.Origin;
                var local = new Vector2(Vector2.Dot(new(offset.X, offset.Y), bindRest.Right), Vector2.Dot(new(offset.X, offset.Y), bindRest.Up));
                // Preserve the part's depth relative to the bound control, including animated control depth.
                cutout[binding.Point] = bindNow.At(local) + new Vector3(0, 0, offset.Z);
            }
            return cutout;
        }
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
    public static float Distance(PuppetPart part, Func<string, Vector3> world, Vector3 point, Func<string, Vector3>? rest = null)
    {
        var p = new Vector2(point.X, point.Y);
        if (PuppetPartKinds.IsStroke(part.Kind))
        {
            var a = world(part.A); var b = world(part.B!);
            return Distance2D.DistanceToSegment(p, new(a.X, a.Y), new(b.X, b.Y), 1e-10f)
                / MathF.Max(part.Width, .06f);
        }
        var frame = FrameOf(part, world);
        var local = p - new Vector2(frame.Origin.X, frame.Origin.Y);
        var coordinates = new Vector2(Vector2.Dot(local, frame.Right), Vector2.Dot(local, frame.Up));
        if (part.Kind == PuppetPartKinds.Polygon)
        {
            var points = Contour(part, world, rest);
            // Folded outlines may overlap. Pick the same rest-topology triangles that are filled, not an even/odd hole.
            if (part.Bindings is { Count: > 0 })
            {
                var mesh = TriangleMesh2D.TriangulateSimplePolygon(part.Points!.Select(v => v.XY), 1e-8);
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
        if (part.Kind == "trapezoid") coordinates.X /= TrapezoidWidthScale(part, coordinates.Y);
        var halfSize = new Vector2(part.Width / 2, part.Height / 2);
        return part.Kind == PuppetPartKinds.Ellipse
            ? Containment2D.NormalizedEllipseRadius(coordinates, Vector2.Zero, halfSize)
            : Containment2D.NormalizedRectangleRadius(coordinates, Vector2.Zero, halfSize);
    }

    /// <summary>Inverse of the same attachment used by rendering, for dragging a point while previewing a pose.</summary>
    public static PuppetPoint ArtworkPoint(PuppetPart part, int index, Vector3 target, Func<string, Vector3> world, Func<string, Vector3> rest)
    {
        var binding = part.Bindings?.FirstOrDefault(b => b.Point == index);
        var art = FrameOf(part, binding is null ? world : rest);
        if (binding is not null)
        {
            var now = BindingFrame(binding, world); var before = BindingFrame(binding, rest);
            var offset = target - now.Origin;
            target = before.At(new(Vector2.Dot(new(offset.X, offset.Y), now.Right), Vector2.Dot(new(offset.X, offset.Y), now.Up)));
        }
        var delta = target - art.Origin;
        return new(Vector2.Dot(new(delta.X, delta.Y), art.Right) / part.Width, Vector2.Dot(new(delta.X, delta.Y), art.Up) / part.Height);
    }

    private static Frame BindingFrame(PartPointBinding binding, Func<string, Vector3> world)
    {
        var origin = world(binding.Control);
        var direction = binding.Toward is { } toward ? world(toward) - origin : Vector3.UnitY;
        var up = new Vector2(direction.X, direction.Y);
        up = up.LengthSquared() > 1e-10f ? Vector2.Normalize(up) : Vector2.UnitY;
        return new(origin, new(up.Y, -up.X), up, 0);
    }

    private static float TrapezoidWidthScale(PuppetPart part, float y) =>
        1 + (part.TopWidthScale - 1) * Math.Clamp(y / part.Height + .5f, 0, 1);
}
