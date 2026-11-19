using App2d.Core.Validation;

namespace App2d.Core.Geometry.Construction;

/// <summary>Circle construction from geometric constraints without creating a shape.</summary>
public static class CircleConstruction2D
{
    /// <summary>The circumcircle through three finite non-collinear points.</summary>
    public static (Vector2f Center, float Radius) ThroughPoints(Vector2f a, Vector2f b, Vector2f c)
    {
        Ensure.Finite(a);
        Ensure.Finite(b);
        Ensure.Finite(c);
        var origin = a.ToVector2d();
        var u = b.ToVector2d() - origin;
        var v = c.ToVector2d() - origin;
        var cross = u.Cross(v);
        Ensure.NonZero(cross);

        var offset = (v.LengthSquared() * u - u.LengthSquared() * v).PerpCcw / (2 * cross);
        var center = (origin + offset).ToVector2f();
        var radius = (float)offset.Length();
        Ensure.Finite(center);
        Ensure.Positive(radius);
        return (center, radius);
    }
}
