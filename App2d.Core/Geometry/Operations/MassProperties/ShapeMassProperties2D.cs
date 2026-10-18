using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.MassProperties;

/// <summary>Exact area, centroid, mass and centroidal polar inertia for uniform-density solid 2D shapes.</summary>
public static class ShapeMassProperties2D
{
    /// <param name="shape">A validated finite shape in its own coordinates, before the object's transform.</param>
    /// <param name="density">Positive mass per square unit; a constant thickness is absorbed into this density.</param>
    /// <exception cref="NotSupportedException">The shape is unbounded, unknown, or requests overlap-counted-once union mass.</exception>
    public static MassProperties2D Calculate(IShape2D shape, double density = 1d)
    {
        ArgGuard.ThrowIfNull(shape);
        ArgGuard.Positive(density);
        shape.Validate();
        return shape switch
        {
            Circle2D circle => Ellipse(circle.Center, new Vector2(circle.Radius), density),
            Ellipse2D ellipse => Ellipse(ellipse.Center, ellipse.Radii, density),
            Rectangle2D rectangle => Rectangle(rectangle.Min, rectangle.Max, density),
            AxisAlignedRectangle2D rectangle => Rectangle(rectangle.Min, rectangle.Max, density),
            Triangle2D triangle => Polygon([triangle.A, triangle.B, triangle.C], density),
            ConvexPolygon2D polygon => Polygon(polygon.Vertices, density),
            SimplePolygon2D polygon => Polygon(polygon.Vertices, density),
            Capsule2D capsule => Capsule(capsule, density),
            RoundedRectangle2D rounded => RoundedRectangle(rounded, density),
            CompositeShape2D composite => Composite(composite, density),
            HalfSpace2D => throw new NotSupportedException("An infinite half-space has no finite mass or centroid at positive density."),
            _ => throw new NotSupportedException($"Mass properties are not implemented for {shape.GetType().Name}.")
        };
    }

    private static MassProperties2D Ellipse(Vector2 center, Vector2 radii, double density)
    {
        var a = (double)radii.X;
        var b = (double)radii.Y;
        var area = Math.PI * a * b;
        return Create(area, new(center), area * (a * a + b * b) / 4d, density);
    }

    private static MassProperties2D Rectangle(Vector2 min, Vector2 max, double density)
    {
        var size = new Vector2d(max) - new Vector2d(min);
        var area = size.X * size.Y;
        return Create(area, new Vector2d(min) + size / 2d, area * size.LengthSquared() / 12d, density);
    }

    private static MassProperties2D Capsule(Capsule2D capsule, double density)
    {
        var start = new Vector2d(capsule.Start);
        var spine = new Vector2d(capsule.End) - start;
        // A capsule is a rounded rectangle whose inner rectangle has zero height.
        return RoundedBox(spine.Length() / 2d, 0d, capsule.Radius, start + spine / 2d, density);
    }

    private static MassProperties2D RoundedRectangle(RoundedRectangle2D rounded, double density)
    {
        var min = new Vector2d(rounded.Min);
        var half = (new Vector2d(rounded.Max) - min) / 2d;
        return RoundedBox(half.X - rounded.Radius, half.Y - rounded.Radius,
            rounded.Radius, min + half, density);
    }

    private static MassProperties2D RoundedBox(double a, double b, double r, Vector2d center, double density)
    {
        // Disjoint pieces: the inner rectangle, two pairs of edge strips, and four quarter disks.
        // a and b are the inner half-extents; r is the corner radius.
        var innerArea = 4d * a * b;
        var horizontalArea = 4d * a * r;
        var verticalArea = 4d * b * r;
        var cornerArea = Math.PI * r * r;
        var innerInertia = innerArea * (a * a + b * b) / 3d;
        var horizontalInertia = horizontalArea * ((4d * a * a + r * r) / 12d + (b + r / 2d) * (b + r / 2d));
        var verticalInertia = verticalArea * ((r * r + 4d * b * b) / 12d + (a + r / 2d) * (a + r / 2d));
        var cornerInertia = cornerArea * (r * r / 2d + a * a + b * b) + 8d / 3d * r * r * r * (a + b);
        return Create(innerArea + horizontalArea + verticalArea + cornerArea, center,
            innerInertia + horizontalInertia + verticalInertia + cornerInertia, density);
    }

    private static MassProperties2D Polygon(ReadOnlySpan<Vector2> vertices, double density)
    {
        // Shift before summation so a small polygon far from the origin keeps its precision.
        var origin = new Vector2d(vertices[0]);
        var areaTwice = 0d;
        var firstMoment = Vector2d.Zero;
        for (var i = 0; i < vertices.Length; i++)
        {
            var a = new Vector2d(vertices[i]) - origin;
            var b = new Vector2d(vertices[(i + 1) % vertices.Length]) - origin;
            var cross = a.Cross(b);
            areaTwice += cross;
            firstMoment += (a + b) * cross;
        }
        var offset = firstMoment / (3d * areaTwice);

        // Integrate about the centroid directly, avoiding subtraction of large origin moments.
        var inertia = 0d;
        for (var i = 0; i < vertices.Length; i++)
        {
            var a = (new Vector2d(vertices[i]) - origin) - offset;
            var b = (new Vector2d(vertices[(i + 1) % vertices.Length]) - origin) - offset;
            inertia += a.Cross(b) * (a.Dot(a) + a.Dot(b) + b.Dot(b));
        }
        return Create(Math.Abs(areaTwice) / 2d, origin + offset, Math.Abs(inertia) / 12d, density);
    }

    private static MassProperties2D Composite(CompositeShape2D composite, double density)
    {
        if (composite.Parts.Length == 1)
        {
            return Calculate(composite.Parts[0], density);
        }
        if (!composite.IncludeOverlap)
        {
            throw new NotSupportedException("Union mass properties that count overlap once are not implemented. Use non-overlapping additive parts or a simple polygon outline.");
        }

        var parts = new MassProperties2D[composite.Parts.Length];
        var area = 0d;
        var mass = 0d;
        var firstMoment = Vector2d.Zero;
        var origin = Vector2d.Zero;
        for (var i = 0; i < parts.Length; i++)
        {
            var part = Calculate(composite.Parts[i], density);
            parts[i] = part;
            if (i == 0)
            {
                origin = part.CenterOfMass;
            }
            area += part.Area;
            mass += part.Mass;
            firstMoment += (part.CenterOfMass - origin) * part.Mass;
        }
        var center = origin + firstMoment / mass;
        var inertia = 0d;
        foreach (var part in parts)
        {
            inertia += part.MomentOfInertiaAbout(center);
        }
        return new(ArgGuard.Positive(area), ArgGuard.Positive(mass), center, ArgGuard.Positive(inertia));
    }

    private static MassProperties2D Create(double area, Vector2d center, double polarMomentOfArea, double density)
    {
        ArgGuard.Positive(area);
        ArgGuard.ThrowIf(!center.IsFinite(), "The centroid must be finite.", nameof(center));
        return new(area, ArgGuard.Positive(area * density), center, ArgGuard.Positive(polarMomentOfArea * density));
    }
}
