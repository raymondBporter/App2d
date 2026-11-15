using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.ClosestPoint;

/// <summary>Double-precision arithmetic for linear geometry queries.</summary>
internal static class LinearGeometry2D
{
    internal const float DefaultPointTolerance = 0.00001f;

    /// <summary>The closest point on a line or forward ray, in double precision.</summary>
    internal static Vector2d ClosestPoint(Vector2f point, Vector2f origin, Vector2f direction, bool forwardOnly)
    {
        Ensure.Finite(point);
        Ensure.Finite(origin);
        Ensure.NonZero(direction);
        var delta = point.ToVector2d() - origin.ToVector2d();
        var axis = direction.ToVector2d();
        var parameter = delta.Dot(axis) / axis.LengthSquared();
        if (forwardOnly)
        {
            parameter = Math.Max(0, parameter);
        }

        return origin.ToVector2d() + axis * parameter;
    }

    /// <summary>The squared distance to a line or forward ray, in double precision.</summary>
    internal static double DistanceSquared(Vector2f point, Vector2f origin, Vector2f direction, bool forwardOnly)
    {
        Ensure.Finite(point);
        Ensure.Finite(origin);
        Ensure.NonZero(direction);
        var delta = point.ToVector2d() - origin.ToVector2d();
        var axis = direction.ToVector2d();
        if (forwardOnly && delta.Dot(axis) < 0)
        {
            return delta.LengthSquared();
        }

        var cross = delta.Cross(axis);
        return cross * cross / axis.LengthSquared();
    }

    /// <summary>The distance to a line or forward ray, in double precision.</summary>
    internal static double Distance(Vector2f point, Vector2f origin, Vector2f direction, bool forwardOnly)
        => Math.Sqrt(DistanceSquared(point, origin, direction, forwardOnly));

    /// <summary>The squared distance to a closed segment, including a segment collapsed to a point.</summary>
    internal static double DistanceSquaredToSegment(Vector2d point, Vector2d start, Vector2d end)
    {
        var axis = end - start;
        var offset = point - start;
        if (offset.Dot(axis) <= 0d)
        {
            return offset.LengthSquared();
        }

        var fromEnd = point - end;
        if (fromEnd.Dot(axis) >= 0d)
        {
            return fromEnd.LengthSquared();
        }

        if (axis.Y == 0d)
        {
            return offset.Y * offset.Y;
        }

        if (axis.X == 0d)
        {
            return offset.X * offset.X;
        }

        var cross = offset.Cross(axis);
        return cross * cross / axis.LengthSquared();
    }
}
