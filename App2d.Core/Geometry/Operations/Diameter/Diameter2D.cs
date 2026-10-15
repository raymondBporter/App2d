using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.Diameter;

/// <summary>Maximum distances between points of raw, filled primitives. Intermediates use double precision.</summary>
public static class Diameter2D
{
    public static float Circle(float radius) => 2 * radius;
    public static float Ellipse(Vector2 radii) => 2 * MathF.Max(radii.X, radii.Y);
    public static float Capsule(Vector2 start, Vector2 end, float radius) => (float)(Vector2d.Distance(start.ToVector2d(), end.ToVector2d()) + 2d * radius);
    public static float Rectangle(Vector2 min, Vector2 max) => (float)Vector2d.Distance(min.ToVector2d(), max.ToVector2d());

    /// <summary>The diagonal of the inner rectangle plus a full corner-circle diameter.</summary>
    public static float RoundedRectangle(Vector2 innerMin, Vector2 innerMax, float radius) =>
        (float)(Vector2d.Distance(innerMin.ToVector2d(), innerMax.ToVector2d()) + 2d * radius);

    public static float Triangle(Vector2 a, Vector2 b, Vector2 c)
    {
        var ab = b.ToVector2d() - a.ToVector2d();
        var bc = c.ToVector2d() - b.ToVector2d();
        var ca = a.ToVector2d() - c.ToVector2d();
        return (float)Math.Sqrt(Math.Max(ab.LengthSquared(), Math.Max(bc.LengthSquared(), ca.LengthSquared())));
    }

    /// <summary>The farthest vertex pair, valid for convex or concave polygons in either winding.</summary>
    public static float Polygon(ReadOnlySpan<Vector2> vertices)
    {
        ArgGuard.ThrowIfTooShort(vertices, 1);
        var squared = 0d;
        for (var i = 0; i < vertices.Length; i++)
        {
            for (var j = i + 1; j < vertices.Length; j++)
                squared = Math.Max(squared, Vector2d.DistanceSquared(vertices[i].ToVector2d(), vertices[j].ToVector2d()));
        }

        return (float)Math.Sqrt(squared);
    }
}
