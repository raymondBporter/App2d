using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Circle construction from geometric constraints without creating a shape.</summary>
public static class CircleConstruction2D
{
    /// <summary>The circumcircle through three finite non-collinear points.</summary>
    public static (Vector2 Center, float Radius) ThroughPoints(Vector2 a, Vector2 b, Vector2 c)
    {
        ArgGuard.ThrowIfNotFinite(a); ArgGuard.ThrowIfNotFinite(b); ArgGuard.ThrowIfNotFinite(c);
        var origin = a.ToVector2d();
        var u = b.ToVector2d() - origin;
        var v = c.ToVector2d() - origin;
        var cross = u.Cross(v);
        if (cross == 0) throw new ArgumentException("Points are collinear.");
        var offset = (v.LengthSquared() * u - u.LengthSquared() * v).PerpCcw / (2 * cross);
        var center = (origin + offset).ToVector2();
        var radius = (float)offset.Length();
        ArgGuard.ThrowIfNotFinite(center); ArgGuard.ThrowIfNotFiniteOrNotPositive(radius);
        return (center, radius);
    }
}
