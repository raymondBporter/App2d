using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>
/// Turns a local shape plus a <see cref="Similarity2D"/> pose into the world-space raw parameters that the
/// <see cref="App2d.Core.Geometry"/> functions consume, writes the perimeters and convex cores that
/// <see cref="ShapeDistance2D"/> and the collision table operate on, and bakes simple transforms into new shapes.
/// Adding a shape means adding it here once.
/// </summary>
public static class WorldShape2D
{
    /// <summary>The world center and radius of a posed circle.</summary>
    /// <param name="circle">The local circle.</param>
    /// <param name="pose">The local-to-world similarity.</param>
    /// <returns>The transformed center and the uniformly scaled radius.</returns>
    public static (Vector2 Center, float Radius) Circle(Circle2D circle, Similarity2D pose) => (pose.TransformPoint(circle.Center), circle.Radius * pose.Scale);

    /// <summary>The world spine and radius of a posed capsule.</summary>
    /// <param name="capsule">The local capsule.</param>
    /// <param name="pose">The local-to-world similarity.</param>
    /// <returns>The transformed endpoints and the uniformly scaled radius.</returns>
    public static (Vector2 Start, Vector2 End, float Radius) Capsule(Capsule2D capsule, Similarity2D pose) => (pose.TransformPoint(capsule.Start), pose.TransformPoint(capsule.End), capsule.Radius * pose.Scale);

    /// <summary>The world unit normal and offset of a posed half-space.</summary>
    /// <param name="halfSpace">The local half-space.</param>
    /// <param name="pose">The local-to-world similarity.</param>
    /// <returns>The normalized transformed normal and the offset of the transformed boundary point along it.</returns>
    public static (Vector2 UnitNormal, float Offset) HalfSpace(HalfSpace2D halfSpace, Similarity2D pose)
    {
        // Normals transform by (A⁻¹)ᵀ = A / Scale² for this family, so the direct direction transform is exact once normalized.
        var normal = Vector2.Normalize(pose.TransformDirection(halfSpace.Normal));
        var boundary = pose.TransformPoint(halfSpace.Normal * halfSpace.Offset);
        return (normal, Vector2.Dot(boundary, normal));
    }

    /// <summary>The number of vertices <see cref="WritePerimeter"/> produces for a shape.</summary>
    /// <param name="shape">Any shape.</param>
    /// <returns>4 for rectangles, 3 for triangles, the vertex count for convex polygons, <see cref="Ellipse2D.CollisionSegments"/> for ellipses, and 0 for shapes with no polygonal perimeter.</returns>
    public static int PerimeterVertexCount(IShape2D shape) => shape switch
    {
        Rectangle2D => 4,
        Triangle2D => 3,
        ConvexPolygon2D polygon => polygon.Vertices.Length,
        Ellipse2D => Ellipse2D.CollisionSegments,
        _ => 0
    };

    /// <summary>Writes the local perimeter of a polygonal shape, or nothing for circles, capsules, half-spaces and composites.</summary>
    /// <param name="shape">Any shape.</param>
    /// <param name="vertices">A buffer of at least <see cref="PerimeterVertexCount"/> entries.</param>
    /// <returns>The number of vertices written.</returns>
    public static int WritePerimeter(IShape2D shape, Span<Vector2> vertices)
    {
        switch (shape)
        {
            case Rectangle2D rectangle: rectangle.WriteCorners(vertices); return 4;
            case Triangle2D triangle: triangle.WriteVertices(vertices); return 3;
            case ConvexPolygon2D polygon: polygon.Vertices.CopyTo(vertices); return polygon.Vertices.Length;
            case Ellipse2D ellipse: return ellipse.WriteVertices(vertices[..Ellipse2D.CollisionSegments]);
            default: return 0;
        }
    }

    /// <summary>Writes the perimeter of a polygonal shape transformed into world space.</summary>
    /// <param name="shape">Any shape.</param>
    /// <param name="pose">The local-to-world similarity.</param>
    /// <param name="vertices">A buffer of at least <see cref="PerimeterVertexCount"/> entries.</param>
    /// <returns>The number of vertices written.</returns>
    public static int WriteWorldPerimeter(IShape2D shape, Similarity2D pose, Span<Vector2> vertices)
    {
        var count = WritePerimeter(shape, vertices);
        for (var i = 0; i < count; i++) vertices[i] = pose.TransformPoint(vertices[i]);
        return count;
    }

    /// <summary>The number of vertices <see cref="WriteWorldConvexCore"/> produces: 1 for circles, 2 for capsules, the perimeter count otherwise.</summary>
    /// <param name="shape">Any shape.</param>
    /// <returns>The core vertex count, or 0 for shapes without a convex core.</returns>
    public static int ConvexCoreVertexCount(IShape2D shape) => shape switch
    {
        Circle2D => 1,
        Capsule2D => 2,
        _ => PerimeterVertexCount(shape)
    };

    /// <summary>
    /// Writes the world-space convex core of a shape: a point plus radius for circles, a segment plus radius for capsules,
    /// and the perimeter with zero radius for polygonal shapes. This is the form <see cref="Distance2D.SignedDistanceBetweenConvexPolygons"/> takes.
    /// </summary>
    /// <param name="shape">Any shape.</param>
    /// <param name="pose">The local-to-world similarity.</param>
    /// <param name="vertices">A buffer of at least <see cref="ConvexCoreVertexCount"/> entries.</param>
    /// <param name="radius">The world-space round radius of the core.</param>
    /// <returns>The number of vertices written, or 0 for shapes without a convex core.</returns>
    public static int WriteWorldConvexCore(IShape2D shape, Similarity2D pose, Span<Vector2> vertices, out float radius)
    {
        int count;
        switch (shape)
        {
            case Circle2D circle:
                vertices[0] = circle.Center;
                radius = circle.Radius;
                count = 1;
                break;
            case Capsule2D capsule:
                vertices[0] = capsule.Start;
                vertices[1] = capsule.End;
                radius = capsule.Radius;
                count = 2;
                break;
            default:
                radius = 0f;
                count = WritePerimeter(shape, vertices);
                break;
        }
        for (var i = 0; i < count; i++) vertices[i] = pose.TransformPoint(vertices[i]);
        radius *= pose.Scale;
        return count;
    }

    /// <summary>The number of vertices <see cref="WriteOutline"/> produces: the perimeter count for polygonal shapes, or the sample count for round ones.</summary>
    /// <param name="shape">Any shape.</param>
    /// <param name="roundSegments">Samples around a circle or ellipse; a capsule uses half per cap.</param>
    /// <returns>The outline vertex count, or 0 for half-spaces and composites.</returns>
    public static int OutlineVertexCount(IShape2D shape, int roundSegments) => shape switch
    {
        Circle2D or Ellipse2D => roundSegments,
        Capsule2D => 2 * (roundSegments / 2 + 1),
        _ => PerimeterVertexCount(shape)
    };

    /// <summary>Writes a drawable local perimeter for any bounded shape. Round shapes are sampled; polygonal shapes use their exact perimeter.</summary>
    /// <param name="shape">Any shape.</param>
    /// <param name="vertices">A buffer of at least <see cref="OutlineVertexCount"/> entries.</param>
    /// <param name="roundSegments">Samples around a circle or ellipse, at least three; a capsule uses half per cap.</param>
    /// <returns>The number of vertices written.</returns>
    public static int WriteOutline(IShape2D shape, Span<Vector2> vertices, int roundSegments)
    {
        ArgGuard.ThrowIfNull(shape);
        ArgGuard.ThrowIf(roundSegments < 3, "Round shapes need at least three segments.", nameof(roundSegments));
        return shape switch
        {
            Circle2D circle => VertexGenerator2D.WriteCircle(vertices[..roundSegments], circle.Center, circle.Radius),
            Ellipse2D ellipse => VertexGenerator2D.WriteEllipse(vertices[..roundSegments], ellipse.Center, ellipse.Radii),
            Capsule2D capsule => VertexGenerator2D.WriteCapsule(vertices, capsule.Start, capsule.End, capsule.Radius, roundSegments / 2),
            _ => WritePerimeter(shape, vertices)
        };
    }

    /// <summary>A copy of a built-in convex shape uniformly scaled about the local origin.</summary>
    /// <param name="shape">A built-in convex shape.</param>
    /// <param name="scale">The finite, positive scale factor.</param>
    /// <returns>A new shape of the same type with scaled geometry.</returns>
    /// <exception cref="NotSupportedException">The shape is not a built-in convex type.</exception>
    public static IConvexShape2D Scaled(IConvexShape2D shape, float scale)
    {
        ArgGuard.ThrowIfNull(shape);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(scale);
        return shape switch
        {
            Circle2D circle => new Circle2D(circle.Radius * scale, circle.Center * scale),
            Ellipse2D ellipse => new Ellipse2D(ellipse.Radii * scale, ellipse.Center * scale),
            Capsule2D capsule => new Capsule2D(capsule.Start * scale, capsule.End * scale, capsule.Radius * scale),
            AxisAlignedRectangle2D rectangle => new AxisAlignedRectangle2D(rectangle.Min * scale, rectangle.Max * scale),
            Rectangle2D rectangle => new Rectangle2D(rectangle.Min * scale, rectangle.Max * scale),
            Triangle2D triangle => new Triangle2D(triangle.A * scale, triangle.B * scale, triangle.C * scale),
            ConvexPolygon2D polygon => ScaledPolygon(polygon, scale),
            _ => throw new NotSupportedException($"Cannot scale {shape.GetType().Name}.")
        };
    }

    private static ConvexPolygon2D ScaledPolygon(ConvexPolygon2D polygon, float scale)
    {
        var vertices = polygon.Vertices.ToArray();
        for (var i = 0; i < vertices.Length; i++) vertices[i] *= scale;
        return new ConvexPolygon2D(vertices);
    }
}
