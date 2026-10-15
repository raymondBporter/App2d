using App2d.Core.Geometry.Builders;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Authoring;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Characters;

/// <summary>Shape presets, cutout validation, geometry edits and editor controls for character parts.</summary>
public static class PartGeometryAuthoring2D
{
    public static GeometryDefinition2D Definition(PuppetPart part) => part.Geometry
        ?? throw new InvalidDataException($"Part '{part.Id}' needs typed geometry.");

    /// <summary>Builds a typed definition from an editor shape preset.</summary>
    public static GeometryDefinition2D FromPreset(PuppetPart part)
    {
        if (PuppetPartKinds.IsStroke(part.Kind))
            return new LineCurveDefinition2D { Start = new(0, 0), End = new(0, 1) };
        if (part.Kind == PuppetPartKinds.Ellipse)
            return new Ellipse2D.Builder { Center = new(0, 0), Radii = new(.5f, .5f) };
        if (part.Kind == PuppetPartKinds.Polygon)
            return Polygon([new(-.5f, -.5f), new(.5f, -.5f), new(.5f, .5f), new(-.5f, .5f)]);
        if (part.Kind == PuppetPartKinds.Box)
        {
            return RoundedRectangle2D.Builder.FromSize(part.Width, part.Height,
                Math.Min(part.Width, part.Height) * .125f);
        }

        if (part.Kind == PuppetPartKinds.Trapezoid)
        {
            return Polygon([new Vector2(-.5f, -.5f), new Vector2(.5f, -.5f),
                new Vector2(.35f, .5f), new Vector2(-.35f, .5f)]);
        }

        throw new InvalidDataException($"Unknown editor geometry preset '{part.Kind}'.");
    }

    private static SimplePolygon2D.Builder Polygon(IEnumerable<Vector2> vertices) =>
        new() { Vertices = [.. vertices] };

    public static void SetPolygon(PuppetPart part, IReadOnlyList<PuppetPoint> points)
    {
        CheckCutout(points);
        part.Geometry = Polygon(points.Select(point => point.XY));
        part.Kind = PuppetPartKinds.Polygon;
    }

    public static void ResizeRoundedRectangle(PuppetPart part, float width, float height)
    {
        if (part.Geometry is not RoundedRectangle2D.Builder rounded) return;
        part.Geometry = RoundedRectangle2D.Builder.FromSize(width, height,
            MathF.Min(rounded.Radius, MathF.Min(width, height) / 2f));
    }

    public static void SetRoundedRectangleRadius(PuppetPart part, float radius)
    {
        if (part.Geometry is not RoundedRectangle2D.Builder) return;
        part.Geometry = RoundedRectangle2D.Builder.FromSize(part.Width, part.Height, radius);
    }

    /// <summary>Derives editor controls from typed geometry without changing the saved silhouette.</summary>
    public static void RestoreEditorFields(PuppetPart part)
    {
        switch (part.Geometry)
        {
            case CurveDefinition2D:
                part.Kind = PuppetPartKinds.Stroke;
                break;
            case Ellipse2D.Builder:
                part.Kind = PuppetPartKinds.Ellipse;
                break;
            case Circle2D.Builder:
                part.Kind = PuppetPartKinds.Ellipse;
                break;
            case Rectangle2D.Builder:
                part.Kind = PuppetPartKinds.Box;
                break;
            case RoundedRectangle2D.Builder:
                part.Kind = PuppetPartKinds.Box;
                break;
            case SimplePolygon2D.Builder:
                part.Kind = PuppetPartKinds.Polygon;
                break;
            case ShapeBuilder2D:
                part.Kind = PuppetPartKinds.Polygon;
                break;
        }
    }
    public static void CheckCutout(IReadOnlyList<PuppetPoint>? points)
    {
        if (points is not { Count: >= 3 and <= 64 }) throw new InvalidDataException("A cutout needs 3 to 64 perimeter points.");
        foreach (var point in points)
        {
            point.Check("cutout.point");
            if (point.Z != 0) throw new InvalidDataException("Cutout coordinates are XY only.");
        }
        try { PolygonTriangulation2D.Triangulate(points.Select(p => p.XY), 1e-8); }
        catch (ArgumentException ex) { throw new InvalidDataException("Cutout must be a simple polygon with area: " + ex.Message, ex); }
    }

    /// <summary>Inverse of the same attachment used by rendering, for dragging a point while previewing a pose.</summary>
    public static PuppetPoint ArtworkPoint(PuppetPart part, int index, Vector3 target, Func<string, Vector3> world, Func<string, Vector3> rest, Func<string, float>? angle = null, Func<string, Matrix3x2>? transform = null, Func<string, Matrix3x2>? restTransform = null)
    {
        var binding = part.Bindings?.FirstOrDefault(b => b.Point == index);
        var art = binding is null ? PartAttachment2D.FrameOf(part, world, angle, transform) : PartAttachment2D.FrameOf(part, rest, transform: restTransform);
        if (binding is not null)
        {
            var now = PartAttachment2D.BindingFrame(binding, world); var before = PartAttachment2D.BindingFrame(binding, rest);
            target = before.At(now.Coordinates.ToLocal(new(target.X, target.Y)));
        }
        if (!art.Coordinates.TryToLocal(new(target.X, target.Y), out var local))
            throw new InvalidOperationException("Cannot edit a cutout through a collapsed frame.");
        return new(local.X / part.Width, local.Y / part.Height);
    }

}
