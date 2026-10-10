using App2d.Core.Geometry;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>Local convex cores and drawing outlines in caller-owned buffers. These helpers do not allocate.</summary>
public static class ShapeVertices2D
{
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
            RoundedRectangle2D rounded when rounded.CoreMin == rounded.CoreMax => 1,
            RoundedRectangle2D rounded when rounded.CoreMin.X == rounded.CoreMax.X || rounded.CoreMin.Y == rounded.CoreMax.Y => 2,
            RoundedRectangle2D => 4,
            Rectangle2D => 4,
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
        if (count == 0) throw new NotSupportedException($"A {shape.Kind} has no exact finite polygonal convex core.");
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
                    vertices[0] = rounded.CoreMin;
                }
                else if (count == 2) { vertices[0] = rounded.CoreMin; vertices[1] = rounded.CoreMax; }
                else
                {
                    VertexGenerator2D.WriteRectangle(vertices, rounded.CoreMin, rounded.CoreMax);
                }

                break;
            case Rectangle2D rectangle:
                rectangle.WriteCorners(vertices);
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
    /// The drawing outline vertex count. Round shapes are sampled; rounded rectangles use 36 samples.
    /// Half-spaces, composites and custom shapes return zero.
    /// </summary>
    /// <param name="shape">The shape.</param>
    /// <param name="roundSegments">At least three samples around a circle or ellipse; capsules use half per cap.</param>
    public static int GetOutlineVertCount(this IShape2D shape, int roundSegments = 32)
    {
        ArgGuard.ThrowIfNull(shape);
        ArgGuard.ThrowIf(roundSegments < 3, "Round shapes need at least three segments.", nameof(roundSegments));
        return WorldShape2D.OutlineVertexCount(shape, roundSegments);
    }

    /// <summary>Writes a local drawing outline, without a repeated closing vertex, and returns the written buffer view.</summary>
    /// <param name="shape">A built-in shape with a single finite outline, including ellipses and concave polygons.</param>
    /// <param name="vertices">A buffer of at least <see cref="GetOutlineVertCount"/> entries; extra entries are untouched.</param>
    /// <param name="roundSegments">At least three samples around a circle or ellipse; capsules use half per cap.</param>
    /// <returns>A read-only view into the caller's buffer, valid for the lifetime of that buffer.</returns>
    /// <exception cref="NotSupportedException">The shape has no single finite outline.</exception>
    public static ReadOnlySpan<Vector2> GetOutlineVerts(this IShape2D shape, Span<Vector2> vertices, int roundSegments = 32)
    {
        var count = shape.GetOutlineVertCount(roundSegments);
        if (count == 0) throw new NotSupportedException($"A {shape.Kind} has no single finite outline.");
        ArgGuard.ThrowIfTooShort<Vector2>(vertices, count);
        WorldShape2D.WriteOutline(shape, vertices, roundSegments);
        return vertices[..count];
    }
}
