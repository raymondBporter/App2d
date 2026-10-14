using App2d.Core.Geometry;
using App2d.Core.Curves;
using App2d.Core.Mathematics;
using App2d.Core.Meshes;
using App2d.Core.Shapes;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Characters;

/// <summary>
/// Where a drawing part lies, from a world-position lookup. Drawing, picking and outlines share this one layout, so what the
/// author clicks is exactly what is drawn. Shapes attach to A; B points their local +Y axis, or Frame uses a bone's angle.
/// </summary>
public static class PartGeometry
{
    private static readonly ConditionalWeakTable<GeometryDefinition2D, object> Built = [];

    public static IShape2D ShapeOf(ShapeDefinition2D definition) => (IShape2D)Built.GetValue(definition, key => ((ShapeDefinition2D)key).Build());
    public static ICurve2D CurveOf(CurveDefinition2D definition) => (ICurve2D)Built.GetValue(definition, key => ((CurveDefinition2D)key).Build());

    private static Vector2 ShapeScale(PuppetPart part, IShape2D shape) => shape is RoundedRectangle2D rounded
        ? new(part.Width / (rounded.Max.X - rounded.Min.X), part.Height / (rounded.Max.Y - rounded.Min.Y))
        : new(part.Width, part.Height);

    public static GeometryDefinition2D Definition(PuppetPart part) => part.Geometry
        ?? throw new InvalidDataException($"Part '{part.Id}' needs typed geometry.");

    /// <summary>Builds a typed definition from an editor shape preset.</summary>
    public static GeometryDefinition2D FromPreset(PuppetPart part)
    {
        if (PuppetPartKinds.IsStroke(part.Kind))
            return new LineCurveDefinition2D { Start = new(0, 0), End = new(0, 1) };
        if (part.Kind == PuppetPartKinds.Ellipse)
            return new EllipseShapeDefinition2D { Center = new(0, 0), Radii = new(.5f, .5f) };
        if (part.Kind == PuppetPartKinds.Polygon)
            return Polygon([new(-.5f, -.5f), new(.5f, -.5f), new(.5f, .5f), new(-.5f, .5f)]);
        if (part.Kind == PuppetPartKinds.Box)
        {
            return RoundedRectangleShapeDefinition2D.FromSize(new(part.Width, part.Height),
                Math.Min(part.Width, part.Height) * .125f);
        }

        if (part.Kind == PuppetPartKinds.Trapezoid)
        {
            return Polygon([new Vector2(-.5f, -.5f), new Vector2(.5f, -.5f),
                new Vector2(.35f, .5f), new Vector2(-.35f, .5f)]);
        }

        throw new InvalidDataException($"Unknown editor geometry preset '{part.Kind}'.");
    }

    private static SimplePolygonShapeDefinition2D Polygon(IEnumerable<Vector2> vertices) =>
        new() { Vertices = [.. vertices] };

    public static IReadOnlyList<PuppetPoint> CutoutPoints(PuppetPart part) =>
        ((SimplePolygonShapeDefinition2D)part.Geometry!).Vertices.Select(p => new PuppetPoint(p.X, p.Y)).ToArray();

    public static void SetPolygon(PuppetPart part, IReadOnlyList<PuppetPoint> points)
    {
        CheckCutout(points);
        part.Geometry = Polygon(points.Select(point => point.XY));
        part.Kind = PuppetPartKinds.Polygon;
    }

    public static void ResizeRoundedRectangle(PuppetPart part, float width, float height)
    {
        if (part.Geometry is not RoundedRectangleShapeDefinition2D rounded) return;
        part.Geometry = RoundedRectangleShapeDefinition2D.FromSize(new(width, height),
            MathF.Min(rounded.Radius, MathF.Min(width, height) / 2f));
    }

    public static void SetRoundedRectangleRadius(PuppetPart part, float radius)
    {
        if (part.Geometry is not RoundedRectangleShapeDefinition2D) return;
        part.Geometry = RoundedRectangleShapeDefinition2D.FromSize(new(part.Width, part.Height), radius);
    }

    /// <summary>Derives editor controls from typed geometry without changing the saved silhouette.</summary>
    public static void RestoreEditorFields(PuppetPart part)
    {
        switch (part.Geometry)
        {
            case CurveDefinition2D:
                part.Kind = PuppetPartKinds.Stroke;
                break;
            case EllipseShapeDefinition2D:
                part.Kind = PuppetPartKinds.Ellipse;
                break;
            case CircleShapeDefinition2D:
                part.Kind = PuppetPartKinds.Ellipse;
                break;
            case RectangleShapeDefinition2D:
                part.Kind = PuppetPartKinds.Box;
                break;
            case RoundedRectangleShapeDefinition2D:
                part.Kind = PuppetPartKinds.Box;
                break;
            case SimplePolygonShapeDefinition2D:
                part.Kind = PuppetPartKinds.Polygon;
                break;
            case ShapeDefinition2D:
                part.Kind = PuppetPartKinds.Polygon;
                break;
        }
    }
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

    public static Frame FrameOf(PuppetPart part, Func<string, Vector3> world, Func<string, float>? angle = null, Func<string, Matrix3x2>? transform = null)
    {
        if (part.Frame is { } bone)
        {
            if (transform is not null)
            {
                var matrix = Matrix3x2.CreateScale(part.ScaleX, part.ScaleY) * Matrix3x2.CreateRotation(part.Angle) * transform(bone);
                var offset = Vector2.TransformNormal(new(part.OffsetX, part.OffsetY), transform(bone));
                return new(world(part.A) + new Vector3(offset, 0), new(matrix.M11, matrix.M12), new(matrix.M21, matrix.M22), part.Depth);
            }
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

    public static List<Vector3> Contour(PuppetPart part, Func<string, Vector3> world, Func<string, Vector3> rest) => Contour(part, world, rest: rest, angle: null);
    public static float Distance(PuppetPart part, Func<string, Vector3> world, Vector3 point, Func<string, Vector3> rest) => Distance(part, world, point, rest: rest, angle: null);

    /// <summary>The closed outline of an ellipse, rounded box, trapezoid or cutout, or a stroke's two endpoints.</summary>
    public static List<Vector3> Contour(PuppetPart part, Func<string, Vector3> world, Func<string, float>? angle = null, Func<string, Matrix3x2>? transform = null, Func<string, Vector3>? rest = null, Func<string, Matrix3x2>? restTransform = null)
    {
        if (part.Geometry is CurveDefinition2D curve)
        {
            var a = world(part.A); var b = world(part.B!);
            var across = new Vector2(b.Y - a.Y, a.X - b.X);
            var along = new Vector2(b.X - a.X, b.Y - a.Y);
            var result = new List<Vector3>();
            var geometry = CurveOf(curve);
            var count = geometry switch { LineSegmentCurve2D => 2, PolylineCurve2D polyline => polyline.Points.Length, _ => 33 };
            for (var i = 0; i < count; i++)
            {
                var p = geometry.Evaluate((float)i / (count - 1));
                result.Add(new Vector3(new Vector2(a.X, a.Y) + across * p.X + along * p.Y, a.Z + (b.Z - a.Z) * p.Y + part.Depth));
            }
            return result;
        }
        var frame = FrameOf(part, world, angle, transform);
        if (part.Geometry is SimplePolygonShapeDefinition2D && part.Bindings is { Count: > 0 })
        {
            var cutout = CutoutPoints(part).Select(p => frame.At(new(p.X * part.Width, p.Y * part.Height))).ToList();
            if (rest is null) throw new InvalidOperationException("A bound cutout needs the model's rest pose.");
            var artRest = FrameOf(part, rest, transform: restTransform);
            foreach (var binding in part.Bindings)
            {
                var point = CutoutPoints(part)[binding.Point];
                var drawnRest = artRest.At(new(point.X * part.Width, point.Y * part.Height));
                var bindRest = BindingFrame(binding, rest); var bindNow = BindingFrame(binding, world);
                var offset = drawnRest - bindRest.Origin;
                var local = new Vector2(Vector2.Dot(new(offset.X, offset.Y), bindRest.Right), Vector2.Dot(new(offset.X, offset.Y), bindRest.Up));
                // Preserve the part's depth relative to the bound control, including animated control depth.
                cutout[binding.Point] = bindNow.At(local) + new Vector3(0, 0, offset.Z);
            }
            return cutout;
        }
        if (part.Geometry is ShapeDefinition2D typed)
        {
            var shape = ShapeOf(typed);
            var count = shape.GetOutlineVertCount(48);
            if (count == 0) throw new NotSupportedException($"A {typed.Kind} has no drawable outline.");
            Span<Vector2> buffer = count <= 64 ? stackalloc Vector2[count] : new Vector2[count];
            var outline = shape.GetOutlineVerts(buffer, 48);
            var typedFrame = FrameOf(part, world, angle, transform);
            var scale = ShapeScale(part, shape);
            var result = new List<Vector3>(count);
            foreach (var vertex in outline) result.Add(typedFrame.At(vertex * scale));
            return result;
        }
        throw new InvalidDataException($"Part '{part.Id}' needs typed geometry.");
    }

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
            var distance = CurveOf(curve).Distance(local) * MathF.Sqrt(lengthSquared);
            return distance / MathF.Max(part.Width, .06f);
        }
        if (part.Geometry is SimplePolygonShapeDefinition2D && part.Bindings is { Count: > 0 })
        {
            var points = Contour(part, world, angle, transform, rest, restTransform);
            // Folded outlines may overlap. Pick the same rest-topology triangles that are filled, not an even/odd hole.
            if (part.Bindings is { Count: > 0 })
            {
                var mesh = TriangleMesh2D.TriangulateSimplePolygon(CutoutPoints(part).Select(v => v.XY), 1e-8);
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
        if (part.Geometry is ShapeDefinition2D typed)
        {
            var typedFrame = FrameOf(part, world, angle, transform);
            var delta = p - new Vector2(typedFrame.Origin.X, typedFrame.Origin.Y);
            var shape = ShapeOf(typed);
            var scale = ShapeScale(part, shape);
            var basis = new Matrix3x2(typedFrame.Right.X, typedFrame.Right.Y, typedFrame.Up.X, typedFrame.Up.Y, 0, 0);
            if (!Matrix3x2.Invert(basis, out var inverse)) return float.PositiveInfinity;
            var normalized = Vector2.TransformNormal(delta, inverse) / scale;
            return shape.ContainsPoint(normalized) ? .5f : 1 + ShapeDistance2D.Distance(normalized, shape);
        }
        throw new InvalidDataException($"Part '{part.Id}' needs typed geometry.");
    }

    /// <summary>Inverse of the same attachment used by rendering, for dragging a point while previewing a pose.</summary>
    public static PuppetPoint ArtworkPoint(PuppetPart part, int index, Vector3 target, Func<string, Vector3> world, Func<string, Vector3> rest, Func<string, float>? angle = null, Func<string, Matrix3x2>? transform = null, Func<string, Matrix3x2>? restTransform = null)
    {
        var binding = part.Bindings?.FirstOrDefault(b => b.Point == index);
        var art = binding is null ? FrameOf(part, world, angle, transform) : FrameOf(part, rest, transform: restTransform);
        if (binding is not null)
        {
            var now = BindingFrame(binding, world); var before = BindingFrame(binding, rest);
            var offset = target - now.Origin;
            target = before.At(new(Vector2.Dot(new(offset.X, offset.Y), now.Right), Vector2.Dot(new(offset.X, offset.Y), now.Up)));
        }
        var delta = target - art.Origin;
        var axes = new Matrix3x2(art.Right.X, art.Right.Y, art.Up.X, art.Up.Y, 0, 0);
        if (!Matrix3x2.Invert(axes, out var inverse)) throw new InvalidOperationException("Cannot edit a cutout through a collapsed frame.");
        var local = Vector2.TransformNormal(new(delta.X, delta.Y), inverse);
        return new(local.X / part.Width, local.Y / part.Height);
    }

    private static Frame BindingFrame(PartPointBinding binding, Func<string, Vector3> world)
    {
        var origin = world(binding.Control);
        var direction = binding.Toward is { } toward ? world(toward) - origin : Vector3.UnitY;
        var up = new Vector2(direction.X, direction.Y);
        up = up.LengthSquared() > 1e-10f ? Vector2.Normalize(up) : Vector2.UnitY;
        return new(origin, new(up.Y, -up.X), up, 0);
    }


}
