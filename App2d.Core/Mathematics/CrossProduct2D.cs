using System.Numerics;

namespace App2d.Core.Mathematics;

/// <summary>Signed 2D cross products with double intermediates for geometric predicates.</summary>
public static class CrossProduct2D
{
    /// <summary>The perpendicular dot product of two vectors.</summary>
    public static double Of(double firstX, double firstY, double secondX, double secondY) =>
        firstX * secondY - firstY * secondX;

    public static double Of(Vector2 first, Vector2 second) =>
        Of(first.X, first.Y, second.X, second.Y);

    /// <summary>Twice the signed area of triangle ABC: positive when A → B → C turns counter-clockwise.</summary>
    public static double Orientation(Vector2 a, Vector2 b, Vector2 c) =>
        Of((double)b.X - a.X, (double)b.Y - a.Y, (double)c.X - a.X, (double)c.Y - a.Y);
}
