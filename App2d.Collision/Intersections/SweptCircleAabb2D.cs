using App2d.Core;
using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Collision.Intersections;

public static class SweptCircleAabb2D
{
    private const float ParallelEpsilon = 1e-7f;

    public static bool TryIntersect(Vector2 start, Vector2 end, float radius, Bounds2D bounds, out SweptCircleHit2D hit)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(radius);
        if (!bounds.IsFinite)
        {
            hit = default;
            return false;
        }

        var expandedMin = bounds.Min - new Vector2(radius);
        var expandedMax = bounds.Max + new Vector2(radius);
        if (start.X >= expandedMin.X && start.X <= expandedMax.X &&
            start.Y >= expandedMin.Y && start.Y <= expandedMax.Y)
        {
            var normal = GetNearestBoundaryNormal(start, expandedMin, expandedMax);
            var initialSurfacePoint = Vector2.Clamp(start - normal * radius, bounds.Min, bounds.Max);
            hit = new SweptCircleHit2D(0f, start, initialSurfacePoint, normal);
            return true;
        }

        var delta = end - start;
        var entryTime = 0f;
        var exitTime = 1f;
        var entryNormal = Vector2.Zero;

        if (!UpdateSlab(start.X, delta.X, expandedMin.X, expandedMax.X, -Vector2.UnitX, Vector2.UnitX, ref entryTime, ref exitTime, ref entryNormal) ||
            !UpdateSlab(start.Y, delta.Y, expandedMin.Y, expandedMax.Y, -Vector2.UnitY, Vector2.UnitY, ref entryTime, ref exitTime, ref entryNormal) ||
            entryTime < 0f || entryTime > 1f || entryNormal == Vector2.Zero)
        {
            hit = default;
            return false;
        }

        var center = Vector2.Lerp(start, end, entryTime);
        var surfacePoint = center - entryNormal * radius;
        surfacePoint = Vector2.Clamp(surfacePoint, bounds.Min, bounds.Max);
        hit = new SweptCircleHit2D(entryTime, center, surfacePoint, entryNormal);
        return true;
    }

    private static bool UpdateSlab(float start, float delta, float minimum, float maximum, Vector2 minimumNormal, Vector2 maximumNormal, ref float entryTime, ref float exitTime, ref Vector2 entryNormal)
    {
        if (MathF.Abs(delta) <= ParallelEpsilon)
            return start >= minimum && start <= maximum;

        var firstTime = (minimum - start) / delta;
        var secondTime = (maximum - start) / delta;
        var firstNormal = minimumNormal;
        if (firstTime > secondTime)
        {
            (firstTime, secondTime) = (secondTime, firstTime);
            firstNormal = maximumNormal;
        }

        if (firstTime >= entryTime)
        {
            entryTime = firstTime;
            entryNormal = firstNormal;
        }

        exitTime = Math.Min(exitTime, secondTime);
        return entryTime <= exitTime;
    }

    private static Vector2 GetNearestBoundaryNormal(Vector2 point, Vector2 minimum, Vector2 maximum)
    {
        var nearestDistance = point.X - minimum.X;
        var nearestNormal = -Vector2.UnitX;
        if (maximum.X - point.X < nearestDistance)
        {
            nearestDistance = maximum.X - point.X;
            nearestNormal = Vector2.UnitX;
        }
        if (point.Y - minimum.Y < nearestDistance)
        {
            nearestDistance = point.Y - minimum.Y;
            nearestNormal = -Vector2.UnitY;
        }
        if (maximum.Y - point.Y < nearestDistance)
            nearestNormal = Vector2.UnitY;
        return nearestNormal;
    }
}

public readonly record struct SweptCircleHit2D(float Time, Vector2 Center, Vector2 SurfacePoint, Vector2 Normal);
