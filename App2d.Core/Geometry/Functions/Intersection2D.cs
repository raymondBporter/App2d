using App2d.Core.Geometry;
using App2d.Core.Validation;
using App2d.Core.Mathematics;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Core.Geometry.Functions;

/// <summary>
/// Intersections in one coordinate space. Includes touching origins and collinear overlap.
/// Uses double intermediates and exact parallel tests; no angular tolerance discards distant crossings.
/// </summary>
public static class Intersection2D
{
    public static bool Intersects(this Line2D first, Line2D second) =>
        Intersects(first.Origin, first.Direction, false, second.Origin, second.Direction, false);

    public static bool Intersects(this Line2D line, Ray2D ray) =>
        Intersects(line.Origin, line.Direction, false, ray.Origin, ray.Direction, true);

    public static bool Intersects(this Ray2D ray, Line2D line) => Intersects(line, ray);

    public static bool Intersects(this Ray2D first, Ray2D second) =>
        Intersects(first.Origin, first.Direction, true, second.Origin, second.Direction, true);

    private static bool Intersects(Vector2 firstOrigin, Vector2 firstDirection, bool firstIsRay,
        Vector2 secondOrigin, Vector2 secondDirection, bool secondIsRay)
    {
        ArgGuard.ThrowIfNotFiniteOrZero(firstDirection);
        ArgGuard.ThrowIfNotFiniteOrZero(secondDirection);
        var dx = (double)secondOrigin.X - firstOrigin.X;
        var dy = (double)secondOrigin.Y - firstOrigin.Y;
        var determinant = CrossProduct2D.Of(firstDirection.X, firstDirection.Y, secondDirection.X, secondDirection.Y);
        var firstNumerator = CrossProduct2D.Of(dx, dy, secondDirection.X, secondDirection.Y);
        var secondNumerator = CrossProduct2D.Of(dx, dy, firstDirection.X, firstDirection.Y);
        if (determinant != 0)
            return (!firstIsRay || firstNumerator / determinant >= 0) &&
                   (!secondIsRay || secondNumerator / determinant >= 0);

        if (secondNumerator != 0) return false; // Parallel, distinct supporting lines.
        if (!firstIsRay || !secondIsRay) return true;
        // Collinear rays overlap if they face the same way or face toward one another.
        var sameDirection = (double)firstDirection.X * secondDirection.X + (double)firstDirection.Y * secondDirection.Y > 0;
        return sameDirection || dx * firstDirection.X + dy * firstDirection.Y >= 0;
    }

}
