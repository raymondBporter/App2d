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
            return RoundedRectangleShapeDefinition2D.FromSize(new(part.Width, part.Height),
                Math.Min(part.Width, part.Height) * .125f);
        if (part.Kind == PuppetPartKinds.Trapezoid)
            return Polygon([new Vector2(-.5f, -.5f), new Vector2(.5f, -.5f),
                new Vector2(.35f, .5f), new Vector2(-.35f, .5f)]);
        throw new InvalidDataException($"Unknown editor geometry preset '{part.Kind}'.");
    }

    private static SimplePolygonShapeDefinition2D Polygon(IEnumerable<Vector2> vertices) =>
        new() { Vertices = [.. vertices.Select(Point2D.From)] };

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
                result.Add(new Vector3(new Vector2(a.X, a.Y) + across * p.X + along * p.Y,
                    a.Z + (b.Z - a.Z) * p.Y + part.Depth));
            }
            return result;
        }
        if (part.Geometry is ShapeDefinition2D typed)
        {
            var shape = ShapeOf(typed);
            var count = WorldShape2D.OutlineVertexCount(shape, 48);
            if (count == 0) throw new NotSupportedException($"A {typed.Kind} has no drawable outline.");
            var outline = new Vector2[count];
            WorldShape2D.WriteOutline(shape, outline, 48);
            var typedFrame = FrameOf(part, world, angle);
            var scale = ShapeScale(part, shape);
            return [.. outline.Select(vertex => typedFrame.At(vertex * scale))];
        }
        throw new InvalidDataException($"Part '{part.Id}' needs typed geometry.");
    }

    /// <summary>
    /// Normalized XY picking score, not a distance in world units. Ellipses use radial distance;
    /// rounded boxes retain their bounding-box score with taper accounted for; strokes use width with a minimum picking tolerance.
    /// </summary>
    public static float Distance(PuppetPart part, Func<string, Vector3> world, Vector3 point, Func<string, float>? angle = null)
    {
        var p = new Vector2(point.X, point.Y);
        if (part.Geometry is CurveDefinition2D)
        {
            var contour = Contour(part, world, angle);
            var distance = float.MaxValue;
            for (var i = 1; i < contour.Count; i++)
                distance = MathF.Min(distance, Distance2D.DistanceToSegment(p,
                    new(contour[i - 1].X, contour[i - 1].Y), new(contour[i].X, contour[i].Y), 1e-10f));
            return distance / MathF.Max(part.Width, .06f);
        }
        if (part.Geometry is ShapeDefinition2D typed)
        {
            var typedFrame = FrameOf(part, world, angle);
            var delta = p - new Vector2(typedFrame.Origin.X, typedFrame.Origin.Y);
            var shape = ShapeOf(typed);
            var scale = ShapeScale(part, shape);
            var normalized = new Vector2(Vector2.Dot(delta, typedFrame.Right) / scale.X,
                Vector2.Dot(delta, typedFrame.Up) / scale.Y);
            return shape.ContainsPoint(normalized) ? .5f : 1 + ShapeDistance2D.Distance(normalized, shape);
        }
        throw new InvalidDataException($"Part '{part.Id}' needs typed geometry.");
    }
}
