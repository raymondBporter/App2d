using App2d.Core.Geometry.Primitives;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.Intersection;

public static partial class Raycast2D
{
    /// <summary>Intersects an infinite line. Collinear overlap misses; the normal is counterclockwise from the target line.</summary>
    public static bool TryLine(Vector2 origin, Vector2 direction, Line2D line, float maxDistance, out RayHit2D hit)
    {
        line.Validate();
        return TryLinear(origin, direction, line.Origin, line.Direction, false, maxDistance, out hit);
    }

    /// <summary>Intersects another forward ray. Collinear overlap misses; the normal is counterclockwise from the target ray.</summary>
    public static bool TryRay(Vector2 origin, Vector2 direction, Ray2D target, float maxDistance, out RayHit2D hit)
    {
        target.Validate();
        return TryLinear(origin, direction, target.Origin, target.Direction, true, maxDistance, out hit);
    }

    private static bool TryLinear(Vector2 origin, Vector2 direction, Vector2 targetOrigin, Vector2 targetDirection,
        bool forwardOnly, float maxDistance, out RayHit2D hit)
    {
        var axis = direction.ToVector2d();
        var targetAxis = targetDirection.ToVector2d();
        var denominator = axis.Cross(targetAxis);
        if (denominator == 0) return Miss(out hit);
        var offset = targetOrigin.ToVector2d() - origin.ToVector2d();
        var distance = offset.Cross(targetAxis) / denominator;
        var alongTarget = offset.Cross(axis) / denominator;
        if (distance < 0 || distance > float.MaxValue || distance > maxDistance || forwardOnly && alongTarget < 0) return Miss(out hit);
        hit = new((origin.ToVector2d() + axis * distance).ToVector2(), targetAxis.PerpCcw.ToVector2(), (float)distance);
        return true;
    }
}
