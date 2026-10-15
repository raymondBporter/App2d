using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Double-precision arithmetic shared by the public line and ray closest-point and distance queries.</summary>
internal static class LinearGeometry2D
{
    internal const float DefaultPointTolerance = 0.00001f;

    /// <summary>The closest point on a line or forward ray, in double precision.</summary>
    internal static (double X, double Y) ClosestPoint(Vector2 point, Vector2 origin, Vector2 direction, bool forwardOnly)
    {
        ArgGuard.ThrowIfNotFinite(point);
        ArgGuard.ThrowIfNotFinite(origin);
        ArgGuard.ThrowIfNotFiniteOrZero(direction);
        var dx = (double)point.X - origin.X;
        var dy = (double)point.Y - origin.Y;
        var lengthSquared = (double)direction.X * direction.X + (double)direction.Y * direction.Y;
        var parameter = (dx * direction.X + dy * direction.Y) / lengthSquared;
        if (forwardOnly) parameter = Math.Max(0, parameter);
        return (origin.X + direction.X * parameter, origin.Y + direction.Y * parameter);
    }

    /// <summary>The squared distance to a line or forward ray, in double precision.</summary>
    internal static double DistanceSquared(Vector2 point, Vector2 origin, Vector2 direction, bool forwardOnly)
    {
        ArgGuard.ThrowIfNotFinite(point);
        ArgGuard.ThrowIfNotFinite(origin);
        ArgGuard.ThrowIfNotFiniteOrZero(direction);
        var dx = (double)point.X - origin.X;
        var dy = (double)point.Y - origin.Y;
        if (forwardOnly && dx * direction.X + dy * direction.Y < 0) return dx * dx + dy * dy;
        var cross = dx * direction.Y - dy * direction.X;
        return cross * cross / ((double)direction.X * direction.X + (double)direction.Y * direction.Y);
    }

    /// <summary>The distance to a line or forward ray, in double precision.</summary>
    internal static double Distance(Vector2 point, Vector2 origin, Vector2 direction, bool forwardOnly) => Math.Sqrt(DistanceSquared(point, origin, direction, forwardOnly));
}
