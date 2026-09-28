using System.Numerics;

namespace App2d.Core.Geometry.Functions;

/// <summary>Shared arithmetic for public closest-point, distance and primitive APIs.</summary>
internal static class LinearGeometry2D
{
    internal const float DefaultPointTolerance = 0.00001f;

    internal static Vector2 NormalizeDirection(Vector2 direction)
    {
        ArgGuard.ThrowIfNotFiniteOrZero(direction);
        // Float squares can overflow or underflow even for valid finite nonzero vectors.
        var length = Math.Sqrt((double)direction.X * direction.X + (double)direction.Y * direction.Y);
        return new((float)(direction.X / length), (float)(direction.Y / length));
    }

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

    internal static double Distance(Vector2 point, Vector2 origin, Vector2 direction, bool forwardOnly)
    {
        ArgGuard.ThrowIfNotFinite(point);
        ArgGuard.ThrowIfNotFinite(origin);
        ArgGuard.ThrowIfNotFiniteOrZero(direction);
        var dx = (double)point.X - origin.X;
        var dy = (double)point.Y - origin.Y;
        if (forwardOnly && dx * direction.X + dy * direction.Y < 0)
            return Math.Sqrt(dx * dx + dy * dy);

        var length = Math.Sqrt((double)direction.X * direction.X + (double)direction.Y * direction.Y);
        return Math.Abs(dx * direction.Y - dy * direction.X) / length;
    }
}
