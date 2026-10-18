using App2d.Core.Geometry.Operations.Distance;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.Diameter;

/// <summary>Diameters of filled shapes: sup |a - b| over every pair of points in the shape.</summary>
public static class ShapeDiameter2D
{
    /// <summary>Built-in shapes use analytic formulas or exact vertex pairs; custom convex support mappings use bounded numerical refinement.</summary>
    public static float Calculate(IShape2D shape)
    {
        ArgGuard.ThrowIfNull(shape);
        return shape switch
        {
            Circle2D circle => circle.Diameter,
            Ellipse2D ellipse => ellipse.Diameter,
            Capsule2D capsule => capsule.Diameter,
            Rectangle2D rectangle => rectangle.Diameter,
            AxisAlignedRectangle2D rectangle => rectangle.Diameter,
            RoundedRectangle2D rounded => rounded.Diameter,
            Triangle2D triangle => triangle.Diameter,
            ConvexPolygon2D polygon => polygon.Diameter,
            SimplePolygon2D polygon => polygon.Diameter,
            HeightFieldShape2D field => field.Diameter,
            HalfSpace2D => float.PositiveInfinity,
            CompositeShape2D composite => Composite(composite.Parts),
            IConvexShape2D convex => FarthestDistance2D.BetweenConvex(convex, convex),
            _ => throw new NotSupportedException($"No diameter calculation is registered for {shape.GetType().Name}."),
        };
    }

    private static float Composite(ReadOnlySpan<IShape2D> parts)
    {
        var diameter = 0f;
        for (var i = 0; i < parts.Length; i++)
        {
            diameter = MathF.Max(diameter, parts[i].Diameter);
            for (var j = i + 1; j < parts.Length; j++)
            {
                diameter = MathF.Max(diameter, Between((IConvexShape2D)parts[i], (IConvexShape2D)parts[j]));
            }
        }
        return diameter;
    }

    private static float Between(IConvexShape2D first, IConvexShape2D second)
    {
        if (first is Ellipse2D a && second is Ellipse2D b)
        {
            return FarthestDistance2D.BetweenEllipses(a.Center, a.Radii, b.Center, b.Radii);
        }

        if (first is Ellipse2D ellipse)
        {
            return FromEllipse(ellipse, second);
        }

        if (second is Ellipse2D otherEllipse)
        {
            return FromEllipse(otherEllipse, first);
        }

        var firstCount = first.GetVertCount();
        var secondCount = second.GetVertCount();
        if (firstCount == 0 || secondCount == 0)
        {
            return FarthestDistance2D.BetweenConvex(first, second);
        }

        Span<Vector2> firstBuffer = firstCount <= 64 ? stackalloc Vector2[firstCount] : new Vector2[firstCount];
        Span<Vector2> secondBuffer = secondCount <= 64 ? stackalloc Vector2[secondCount] : new Vector2[secondCount];
        var firstVertices = first.GetVerts(firstBuffer, out var firstRadius);
        var secondVertices = second.GetVerts(secondBuffer, out var secondRadius);
        var squared = 0d;
        foreach (var start in firstVertices)
        {
            foreach (var end in secondVertices)
            {
                squared = Math.Max(squared, Vector2d.DistanceSquared(start.ToVector2d(), end.ToVector2d()));
            }
        }

        return (float)(Math.Sqrt(squared) + firstRadius + secondRadius);
    }

    private static float FromEllipse(Ellipse2D ellipse, IConvexShape2D other)
    {
        var count = other.GetVertCount();
        if (count == 0)
        {
            return FarthestDistance2D.BetweenConvex(ellipse, other);
        }

        Span<Vector2> buffer = count <= 64 ? stackalloc Vector2[count] : new Vector2[count];
        var vertices = other.GetVerts(buffer, out var radius);
        var distance = 0f;
        foreach (var point in vertices)
        {
            distance = MathF.Max(distance, FarthestDistance2D.BetweenEllipses(ellipse.Center, ellipse.Radii, point, Vector2.Zero));
        }

        return distance + radius;
    }
}
