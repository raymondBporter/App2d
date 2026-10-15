using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Double-precision arithmetic shared by the public line and ray closest-point and distance queries.</summary>
internal static class LinearGeometry2D
{
    internal const float DefaultPointTolerance = 0.00001f;

    /// <summary>The closest point on a line or forward ray, in double precision.</summary>
    internal static Vector2d ClosestPoint(Vector2 point, Vector2 origin, Vector2 direction, bool forwardOnly)
    {
        ArgGuard.ThrowIfNotFinite(point);
        ArgGuard.ThrowIfNotFinite(origin);
        ArgGuard.ThrowIfNotFiniteOrZero(direction);
        var delta = point.ToVector2d() - origin.ToVector2d();
        var axis = direction.ToVector2d();
        var parameter = delta.Dot(axis) / axis.LengthSquared();
        if (forwardOnly) parameter = Math.Max(0, parameter);
        return origin.ToVector2d() + axis * parameter;
    }

    /// <summary>The squared distance to a line or forward ray, in double precision.</summary>
    internal static double DistanceSquared(Vector2 point, Vector2 origin, Vector2 direction, bool forwardOnly)
    {
        ArgGuard.ThrowIfNotFinite(point);
        ArgGuard.ThrowIfNotFinite(origin);
        ArgGuard.ThrowIfNotFiniteOrZero(direction);
        var delta = point.ToVector2d() - origin.ToVector2d();
        var axis = direction.ToVector2d();
        if (forwardOnly && delta.Dot(axis) < 0) return delta.LengthSquared();
        var cross = delta.Cross(axis);
        return cross * cross / axis.LengthSquared();
    }

    /// <summary>The distance to a line or forward ray, in double precision.</summary>
    internal static double Distance(Vector2 point, Vector2 origin, Vector2 direction, bool forwardOnly)
        => Math.Sqrt(DistanceSquared(point, origin, direction, forwardOnly));
}
