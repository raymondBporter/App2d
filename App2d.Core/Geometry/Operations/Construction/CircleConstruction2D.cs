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
        var ux = (double)b.X - a.X; var uy = (double)b.Y - a.Y;
        var vx = (double)c.X - a.X; var vy = (double)c.Y - a.Y;
        var cross = ux * vy - uy * vx;
        if (cross == 0) throw new ArgumentException("Points are collinear.");
        var u2 = ux * ux + uy * uy; var v2 = vx * vx + vy * vy;
        var x = (u2 * vy - v2 * uy) / (2 * cross);
        var y = (v2 * ux - u2 * vx) / (2 * cross);
        var center = new Vector2((float)(a.X + x), (float)(a.Y + y));
        var radius = (float)Math.Sqrt(x * x + y * y);
        ArgGuard.ThrowIfNotFinite(center); ArgGuard.ThrowIfNotFiniteOrNotPositive(radius);
        return (center, radius);
    }
}
