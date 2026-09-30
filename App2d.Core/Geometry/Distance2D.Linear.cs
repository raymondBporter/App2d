using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Geometry;

public static partial class Distance2D
{
    /// <summary>Unsigned perpendicular distance to an infinite line. Direction may have any finite nonzero length.</summary>
    public static float DistanceToLine(Vector2 point, Vector2 origin, Vector2 direction) =>
        (float)LinearGeometry2D.Distance(point, origin, direction, forwardOnly: false);

    /// <summary>Unsigned distance to a forward ray, including distance to its origin for points behind it.</summary>
    public static float DistanceToRay(Vector2 point, Vector2 origin, Vector2 direction) =>
        (float)LinearGeometry2D.Distance(point, origin, direction, forwardOnly: true);

    public static float Distance(Vector2 point, Line2D line) => DistanceToLine(point, line.Origin, line.Direction);
    public static float Distance(Line2D line, Vector2 point) => Distance(point, line);
    public static float Distance(Vector2 point, Ray2D ray) => DistanceToRay(point, ray.Origin, ray.Direction);
    public static float Distance(Ray2D ray, Vector2 point) => Distance(point, ray);
}
