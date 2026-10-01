using System.Numerics;

namespace App2d.Core.Geometry;

public static partial class Distance2D
{
    /// <summary>The unsigned perpendicular distance from a point to an infinite line.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="origin">A point on the line.</param>
    /// <param name="direction">A finite, nonzero direction; unit length is not required.</param>
    /// <returns>The distance to the line.</returns>
    public static float DistanceToLine(Vector2 point, Vector2 origin, Vector2 direction) => (float)LinearGeometry2D.Distance(point, origin, direction, forwardOnly: false);

    /// <summary>The squared perpendicular distance from a point to an infinite line.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="origin">A point on the line.</param>
    /// <param name="direction">A finite, nonzero direction; unit length is not required.</param>
    /// <returns>The squared distance to the line.</returns>
    public static float DistanceSquaredToLine(Vector2 point, Vector2 origin, Vector2 direction) => (float)LinearGeometry2D.DistanceSquared(point, origin, direction, forwardOnly: false);

    /// <summary>The unsigned distance from a point to a forward ray, measured to the origin for points behind it.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="origin">The ray origin.</param>
    /// <param name="direction">A finite, nonzero direction; unit length is not required.</param>
    /// <returns>The distance to the ray.</returns>
    public static float DistanceToRay(Vector2 point, Vector2 origin, Vector2 direction) => (float)LinearGeometry2D.Distance(point, origin, direction, forwardOnly: true);

    /// <summary>The squared distance from a point to a forward ray, measured to the origin for points behind it.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="origin">The ray origin.</param>
    /// <param name="direction">A finite, nonzero direction; unit length is not required.</param>
    /// <returns>The squared distance to the ray.</returns>
    public static float DistanceSquaredToRay(Vector2 point, Vector2 origin, Vector2 direction) => (float)LinearGeometry2D.DistanceSquared(point, origin, direction, forwardOnly: true);

    /// <summary>The distance from a point to a line value.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="line">A valid line.</param>
    /// <returns>The perpendicular distance.</returns>
    public static float Distance(Vector2 point, Line2D line) => DistanceToLine(point, line.Origin, line.Direction);

    /// <summary>The distance from a line value to a point; argument order does not matter.</summary>
    /// <param name="line">A valid line.</param>
    /// <param name="point">The query point.</param>
    /// <returns>The perpendicular distance.</returns>
    public static float Distance(Line2D line, Vector2 point) => Distance(point, line);

    /// <summary>The distance from a point to a ray value.</summary>
    /// <param name="point">The query point.</param>
    /// <param name="ray">A valid ray.</param>
    /// <returns>The distance to the forward ray.</returns>
    public static float Distance(Vector2 point, Ray2D ray) => DistanceToRay(point, ray.Origin, ray.Direction);

    /// <summary>The distance from a ray value to a point; argument order does not matter.</summary>
    /// <param name="ray">A valid ray.</param>
    /// <param name="point">The query point.</param>
    /// <returns>The distance to the forward ray.</returns>
    public static float Distance(Ray2D ray, Vector2 point) => Distance(point, ray);
}
