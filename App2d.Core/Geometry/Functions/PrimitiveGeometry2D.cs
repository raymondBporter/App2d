using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>
/// Arithmetic queries on raw primitive parameters, independent of IShape2D.
/// Inputs are finite; radii are nonnegative and ellipse radii/box half-extents are positive.
/// Callers retaining shapes can validate once in their constructors.
/// </summary>
public static class PrimitiveGeometry2D
{
    public static float CircleArea(float radius) => MathF.PI * radius * radius;
    public static float CapsuleArea(Vector2 start, Vector2 end, float radius) =>
        2f * radius * Vector2.Distance(start, end) + CircleArea(radius);
    public static float RectangleArea(Vector2 min, Vector2 max) => (max.X - min.X) * (max.Y - min.Y);

    public static bool CircleContainsPoint(Vector2 point, Vector2 center, float radius) =>
        Vector2.DistanceSquared(point, center) <= radius * radius;
    public static bool CapsuleContainsPoint(Vector2 point, Vector2 start, Vector2 end, float radius) =>
        Vector2.DistanceSquared(point, ClosestPoint2D.OnSegment(point, start, end)) <= radius * radius;
    public static bool RectangleContainsPoint(Vector2 point, Vector2 min, Vector2 max) =>
        point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y;
    public static bool EllipseContainsPoint(Vector2 point, Vector2 center, Vector2 radii) =>
        ((point - center) / radii).LengthSquared() <= 1f;

    public static Vector2 CircleSupportPoint(Vector2 direction, Vector2 center, float radius) =>
        direction.LengthSquared() <= float.Epsilon ? center : center + Vector2.Normalize(direction) * radius;

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
        Vector2.Distance(point, (end - start).LengthSquared() < degenerateLengthSquared
            ? start : ClosestPoint2D.OnSegment(point, start, end));

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
