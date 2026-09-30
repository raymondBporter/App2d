using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>
/// Arithmetic queries on raw primitive parameters, independent of IShape2D.
/// Inputs are finite; radii are nonnegative and ellipse radii/box half-extents are positive.
/// Callers retaining shapes can validate once in their constructors.
/// Rectangle containment and overlap also support ordered infinite bounds.
/// </summary>
public static class PrimitiveGeometry2D
{
    public static float CircleArea(float radius) => MathF.PI * radius * radius;
    public static float EllipseArea(Vector2 radii) => MathF.PI * radii.X * radii.Y;
    public static float CapsuleArea(Vector2 start, Vector2 end, float radius) =>
        2f * radius * Vector2.Distance(start, end) + CircleArea(radius);
    public static float RectangleArea(Vector2 min, Vector2 max) => (max.X - min.X) * (max.Y - min.Y);

    public static bool CircleContainsPoint(Vector2 point, Vector2 center, float radius) =>
        Vector2.DistanceSquared(point, center) <= radius * radius;
    public static bool CapsuleContainsPoint(Vector2 point, Vector2 start, Vector2 end, float radius) =>
        Vector2.DistanceSquared(point, ClosestPoint2D.OnSegment(point, start, end)) <= radius * radius;
    public static bool RectangleContainsPoint(Vector2 point, Vector2 min, Vector2 max) =>
        point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y;

    /// <summary>Inclusive containment of an entire rectangle in another; both sets of bounds are ordered.</summary>
    public static bool RectangleContainsRectangle(Vector2 min, Vector2 max, Vector2 otherMin, Vector2 otherMax) =>
        RectangleContainsPoint(otherMin, min, max) && RectangleContainsPoint(otherMax, min, max);

    /// <summary>Inclusive overlap of ordered rectangle bounds, including edge and corner contact.</summary>
    public static bool RectanglesIntersect(Vector2 firstMin, Vector2 firstMax, Vector2 secondMin, Vector2 secondMax) =>
        firstMin.X <= secondMax.X && firstMax.X >= secondMin.X &&
        firstMin.Y <= secondMax.Y && firstMax.Y >= secondMin.Y;

    public static bool EllipseContainsPoint(Vector2 point, Vector2 center, Vector2 radii) =>
        ((point - center) / radii).LengthSquared() <= 1f;

    public static Vector2 CircleSupportPoint(Vector2 direction, Vector2 center, float radius) =>
        direction.LengthSquared() <= float.Epsilon ? center : center + Vector2.Normalize(direction) * radius;

    /// <summary>Farthest point on an axis-aligned ellipse in a local direction.</summary>
    public static Vector2 EllipseSupportPoint(Vector2 direction, Vector2 center, Vector2 radii)
    {
        var scaled = direction * radii;
        var length = scaled.Length();
        return length <= float.Epsilon ? center : center + radii * (scaled / length);
    }

    public static Vector2 CapsuleSupportPoint(Vector2 direction, Vector2 start, Vector2 end, float radius)
    {
        var endpoint = Vector2.Dot(start, direction) > Vector2.Dot(end, direction) ? start : end;
        return CircleSupportPoint(direction, endpoint, radius);
    }

    public static Vector2 RectangleSupportPoint(Vector2 direction, Vector2 min, Vector2 max) =>
        new(direction.X >= 0f ? max.X : min.X, direction.Y >= 0f ? max.Y : min.Y);

    /// <summary>Euclidean distance in input units, with an optional squared length tolerance for a point-like segment.</summary>
    public static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end,
        float degenerateLengthSquared = float.Epsilon) =>
        Distance2D.DistanceToSegment(point, start, end, degenerateLengthSquared);

    /// <summary>Dimensionless radial score: 0 at center, 1 on the ellipse. Not Euclidean distance to its boundary.</summary>
    public static float NormalizedEllipseRadius(Vector2 point, Vector2 center, Vector2 radii) =>
        ((point - center) / radii).Length();

    /// <summary>Dimensionless radial score: 0 at center, 1 on the box. Half-extents are strictly positive.</summary>
    public static float NormalizedRectangleRadius(Vector2 point, Vector2 center, Vector2 halfExtents)
    {
        var normalized = Vector2.Abs((point - center) / halfExtents);
        return Math.Max(normalized.X, normalized.Y);
    }
}
