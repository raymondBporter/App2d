using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Areas of raw primitives. Inputs are finite; radii and extents are nonnegative.</summary>
public static class Area2D
{
    /// <summary>The area of a circle.</summary>
    /// <param name="radius">The circle radius.</param>
    /// <returns>Pi times the radius squared.</returns>
    public static float Circle(float radius) => MathF.PI * radius * radius;

    /// <summary>The area of an axis-aligned ellipse.</summary>
    /// <param name="radii">The half-extents along X and Y.</param>
    /// <returns>Pi times both half-extents.</returns>
    public static float Ellipse(Vector2 radii) => MathF.PI * radii.X * radii.Y;

    /// <summary>The area of a capsule: a rectangle around the spine plus a full circle of end caps.</summary>
    /// <param name="start">One spine endpoint.</param>
    /// <param name="end">The other spine endpoint.</param>
    /// <param name="radius">The capsule radius.</param>
    /// <returns>Twice the radius times the spine length, plus the circle area.</returns>
    public static float Capsule(Vector2 start, Vector2 end, float radius) => 2f * radius * Vector2.Distance(start, end) + Circle(radius);

    /// <summary>The area of an ordered axis-aligned rectangle.</summary>
    /// <param name="min">The lower-left corner.</param>
    /// <param name="max">The upper-right corner.</param>
    /// <returns>Width times height.</returns>
    public static float Rectangle(Vector2 min, Vector2 max) => (max.X - min.X) * (max.Y - min.Y);

    /// <summary>The unsigned area of a simple polygon given in perimeter order, in either winding.</summary>
    /// <param name="vertices">The perimeter vertices without a repeated closing vertex.</param>
    /// <returns>The enclosed area.</returns>
    public static float Polygon(ReadOnlySpan<Vector2> vertices) => MathF.Abs(PolygonGeometry2D.SignedAreaTwice(vertices)) / 2f;
}
