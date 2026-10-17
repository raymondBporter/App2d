using App2d.Core.Geometry.Shapes;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.Vertices;

/// <summary>Local convex cores and drawing outlines. Span overloads write into caller-owned buffers without allocating.</summary>
public static class ShapeVertices2D
{
    /// <summary>Samples a placed outline into the caller's buffer without allocating or registering a spatial object.</summary>
    public static ReadOnlySpan<Vector2> GetOutlineVerts(this IShape2D shape, Similarity2D pose, Span<Vector2> vertices, int roundSegments = 32, int cornerSegments = 8)
    {
        var outline = shape.GetOutlineVerts(vertices, roundSegments, cornerSegments);
        for (var i = 0; i < outline.Length; i++)
        {
            vertices[i] = pose.TransformPoint(outline[i]);
        }
        return vertices[..outline.Length];
    }

    /// <summary>Allocates a drawable outline of a placed shape. Use the span overload when supplying reusable storage.</summary>
    public static Vector2[] Outline(this IShape2D shape, Similarity2D pose, int roundSegments = 32, int cornerSegments = 8)
    {
        var vertices = new Vector2[shape.GetOutlineVertCount(roundSegments, cornerSegments)];
        shape.GetOutlineVerts(pose, vertices, roundSegments, cornerSegments);
        return vertices;
    }

    /// <summary>
    /// The exact core vertex count: one point for circles, two endpoints for capsules, inset corners for rounded
    /// rectangles, and perimeter vertices for triangles, rectangles and convex polygons.
    /// Ellipses, concave polygons, composites, half-spaces and custom shapes return zero.
    /// </summary>
    public static int GetVertCount(this IShape2D shape)
    {
        ArgGuard.ThrowIfNull(shape);
        return shape switch
        {
            Circle2D => 1,
            Capsule2D => 2,
            RoundedRectangle2D rounded when rounded.InnerRect.Min == rounded.InnerRect.Max => 1,
            RoundedRectangle2D rounded when rounded.InnerRect.Min.X == rounded.InnerRect.Max.X || rounded.InnerRect.Min.Y == rounded.InnerRect.Max.Y => 2,
            RoundedRectangle2D => 4,
            IRect2D => 4,
            Triangle2D => 3,
            ConvexPolygon2D polygon => polygon.Vertices.Length,
            _ => 0
        };
    }

    /// <summary>
    /// Writes the exact local convex core and returns only the written portion of the buffer.
    /// Expanding this core by <paramref name="radius"/> gives the shape; round edges are never sampled here.
    /// </summary>
    /// <param name="shape">A built-in shape with a finite polygonal convex core.</param>
    /// <param name="vertices">A buffer of at least <see cref="GetVertCount"/> entries; extra entries are untouched.</param>
    /// <param name="radius">The local round radius, or zero for an unrounded polygon.</param>
    /// <returns>A read-only view into the caller's buffer, valid for the lifetime of that buffer.</returns>
    /// <exception cref="NotSupportedException">The shape has no exact finite polygonal convex core.</exception>
    public static ReadOnlySpan<Vector2> GetVerts(this IShape2D shape, Span<Vector2> vertices, out float radius)
    {
        var count = shape.GetVertCount();
        if (count == 0)
        {
            throw new NotSupportedException($"A {shape.Kind} has no exact finite polygonal convex core.");
        }

        ArgGuard.ThrowIfTooShort<Vector2>(vertices, count);
        radius = 0f;
        switch (shape)
        {
            case Circle2D circle:
                vertices[0] = circle.Center;
                radius = circle.Radius;
                break;
            case Capsule2D capsule:
                vertices[0] = capsule.Start;
                vertices[1] = capsule.End;
                radius = capsule.Radius;
                break;
            case RoundedRectangle2D rounded:
                radius = rounded.Radius;
                if (count == 1)
                {
                    vertices[0] = rounded.InnerRect.Min;
                }
                else if (count == 2)
                {
                    vertices[0] = rounded.InnerRect.Min;
                    vertices[1] = rounded.InnerRect.Max;
                }
                else
                {
                    VertexGenerator2D.WriteRectangle(vertices, rounded.InnerRect.Min, rounded.InnerRect.Max);
                }

                break;
            case IRect2D rectangle:
                VertexGenerator2D.WriteRectangle(vertices, rectangle.Min, rectangle.Max);
                break;
            case Triangle2D triangle:
                triangle.WriteVertices(vertices);
                break;
            case ConvexPolygon2D polygon:
                polygon.Vertices.CopyTo(vertices);
                break;
        }
        return vertices[..count];
    }

    /// <summary>
    /// The drawing outline vertex count. Rounded rectangles use four arcs with cornerSegments + 1 samples each.
    /// Half-spaces, composites and custom shapes return zero.
    /// </summary>
    /// <param name="shape">The shape.</param>
    /// <param name="roundSegments">At least three samples around a circle or ellipse; capsules use half per cap.</param>
    public static int GetOutlineVertCount(this IShape2D shape, int roundSegments = 32, int cornerSegments = 8)
    {
        ArgGuard.ThrowIfNull(shape);
        ArgGuard.ThrowIf(roundSegments < 3, "Round shapes need at least three segments.", nameof(roundSegments));
        ArgGuard.Positive(cornerSegments);
        return shape switch
        {
            Circle2D or Ellipse2D => roundSegments,
            Capsule2D => 2 * (roundSegments / 2 + 1),
            RoundedRectangle2D => checked(4 * (cornerSegments + 1)),
            IRect2D => 4,
            Triangle2D => 3,
            ConvexPolygon2D polygon => polygon.Vertices.Length,
            SimplePolygon2D polygon => polygon.Vertices.Length,
            _ => 0
        };
    }

    /// <summary>Writes a local drawing outline, without a repeated closing vertex, and returns the written buffer view.</summary>
    /// <param name="shape">A built-in shape with a single finite outline, including ellipses and concave polygons.</param>
    /// <param name="vertices">A buffer of at least <see cref="GetOutlineVertCount"/> entries; extra entries are untouched.</param>
    /// <param name="roundSegments">At least three samples around a circle or ellipse; capsules use half per cap.</param>
    /// <returns>A read-only view into the caller's buffer, valid for the lifetime of that buffer.</returns>
    /// <exception cref="NotSupportedException">The shape has no single finite outline.</exception>
    public static ReadOnlySpan<Vector2> GetOutlineVerts(this IShape2D shape, Span<Vector2> vertices, int roundSegments = 32, int cornerSegments = 8)
    {
        var count = shape.GetOutlineVertCount(roundSegments, cornerSegments);
        if (count == 0)
        {
            throw new NotSupportedException($"A {shape.Kind} has no single finite outline.");
        }

        ArgGuard.ThrowIfTooShort<Vector2>(vertices, count);
        switch (shape)
        {
            case Circle2D circle:
                VertexGenerator2D.WriteCircle(vertices[..count], circle.Center, circle.Radius);
                break;
            case Ellipse2D ellipse:
                VertexGenerator2D.WriteEllipse(vertices[..count], ellipse.Center, ellipse.Radii);
                break;
            case Capsule2D capsule:
                VertexGenerator2D.WriteCapsule(vertices, capsule.Start, capsule.End, capsule.Radius, roundSegments / 2);
                break;
            case RoundedRectangle2D rounded:
                VertexGenerator2D.WriteRoundedRectangle(vertices, rounded.OuterRect.Min, rounded.OuterRect.Max, rounded.Radius, cornerSegments);
                break;
            case IRect2D rectangle:
                VertexGenerator2D.WriteRectangle(vertices, rectangle.Min, rectangle.Max);
                break;
            case Triangle2D triangle:
                triangle.WriteVertices(vertices);
                break;
            case ConvexPolygon2D polygon:
                polygon.Vertices.CopyTo(vertices);
                break;
            case SimplePolygon2D polygon:
                polygon.Vertices.CopyTo(vertices);
                break;
        }
        return vertices[..count];
    }
}
