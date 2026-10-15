using System.Numerics;

namespace App2d.Core.Geometry;

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
    public static Vector2 Circle(Vector2 direction, Vector2 center, float radius) => direction.LengthSquared() <= float.Epsilon ? center : center + Vector2.Normalize(direction) * radius;

    /// <summary>The farthest point of an axis-aligned ellipse in a direction.</summary>
    /// <param name="direction">The query direction.</param>
    /// <param name="center">The ellipse center.</param>
    /// <param name="radii">The positive half-extents along X and Y.</param>
    /// <returns>The boundary point maximizing the projection, or the center for a zero direction.</returns>
    public static Vector2 Ellipse(Vector2 direction, Vector2 center, Vector2 radii)
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
    public static Vector2 Capsule(Vector2 direction, Vector2 start, Vector2 end, float radius)
    {
        var endpoint = Vector2.Dot(start, direction) > Vector2.Dot(end, direction) ? start : end;
        return Circle(direction, endpoint, radius);
    }

    /// <summary>The farthest corner of an ordered axis-aligned rectangle in a direction.</summary>
    /// <param name="direction">The query direction.</param>
    /// <param name="min">The lower-left corner.</param>
    /// <param name="max">The upper-right corner.</param>
    /// <returns>The corner selected component-wise by the signs of the direction.</returns>
    public static Vector2 Rectangle(Vector2 direction, Vector2 min, Vector2 max) => new(direction.X >= 0f ? max.X : min.X, direction.Y >= 0f ? max.Y : min.Y);

    /// <summary>The vertex of a polygon with the largest projection onto a direction.</summary>
    /// <param name="direction">The query direction.</param>
    /// <param name="vertices">At least one vertex.</param>
    /// <returns>The first vertex achieving the maximum projection.</returns>
    public static Vector2 Polygon(Vector2 direction, ReadOnlySpan<Vector2> vertices)
    {
        var support = vertices[0];
        var bestProjection = Vector2.Dot(support, direction);
        foreach (var vertex in vertices[1..])
        {
            var projection = Vector2.Dot(vertex, direction);
            if (projection > bestProjection)
            {
                bestProjection = projection;
                support = vertex;
            }
        }
        return support;
    }
}
