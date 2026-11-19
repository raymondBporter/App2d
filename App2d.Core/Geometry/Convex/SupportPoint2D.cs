namespace App2d.Core.Geometry.Convex;

/// <summary>
/// Farthest points of raw convex primitives in a direction. A zero direction returns a representative point.
/// Directions need not be unit length. These drive half-space contacts and generic convex bounds.
/// </summary>
public static class SupportPoint2D
{
    /// <summary>The farthest point of a circle in a direction.</summary>
    /// <param name="direction">The query direction.</param>
    /// <param name="center">The circle center.</param>
    /// <param name="radius">The circle radius.</param>
    /// <returns>The center moved one radius along the normalized direction, or the center for a zero direction.</returns>
    public static Vector2f Circle(Vector2f direction, Vector2f center, float radius) => direction.LengthSquared() <= float.Epsilon ? center : center + Vector2f.Normalize(direction) * radius;

    /// <summary>The farthest point of an axis-aligned ellipse in a direction.</summary>
    /// <param name="direction">The query direction.</param>
    /// <param name="center">The ellipse center.</param>
    /// <param name="radii">The positive half-extents along X and Y.</param>
    /// <returns>The boundary point maximizing the projection, or the center for a zero direction.</returns>
    public static Vector2f Ellipse(Vector2f direction, Vector2f center, Vector2f radii)
    {
        var scaled = direction * radii;
        var length = scaled.Length();
        return length <= float.Epsilon ? center : center + radii * (scaled / length);
    }

    /// <summary>The farthest point of a capsule in a direction.</summary>
    /// <param name="direction">The query direction.</param>
    /// <param name="start">One spine endpoint.</param>
    /// <param name="end">The other spine endpoint.</param>
    /// <param name="radius">The capsule radius.</param>
    /// <returns>The support point of the cap around the farther spine endpoint.</returns>
    public static Vector2f Capsule(Vector2f direction, Vector2f start, Vector2f end, float radius)
    {
        var endpoint = Vector2f.Dot(start, direction) > Vector2f.Dot(end, direction) ? start : end;
        return Circle(direction, endpoint, radius);
    }

    /// <summary>The farthest corner of an ordered axis-aligned rectangle in a direction.</summary>
    /// <param name="direction">The query direction.</param>
    /// <param name="min">The lower-left corner.</param>
    /// <param name="max">The upper-right corner.</param>
    /// <returns>The corner selected component-wise by the signs of the direction.</returns>
    public static Vector2f Rectangle(Vector2f direction, Vector2f min, Vector2f max) => new(direction.X >= 0f ? max.X : min.X, direction.Y >= 0f ? max.Y : min.Y);

    /// <summary>The vertex of a polygon with the largest projection onto a direction.</summary>
    /// <param name="direction">The query direction.</param>
    /// <param name="vertices">At least one vertex.</param>
    /// <returns>The first vertex achieving the maximum projection.</returns>
    public static Vector2f Polygon(Vector2f direction, ReadOnlySpan<Vector2f> vertices)
    {
        var support = vertices[0];
        var bestProjection = Vector2f.Dot(support, direction);
        foreach (var vertex in vertices[1..])
        {
            var projection = Vector2f.Dot(vertex, direction);
            if (projection > bestProjection)
            {
                bestProjection = projection;
                support = vertex;
            }
        }
        return support;
    }
}
