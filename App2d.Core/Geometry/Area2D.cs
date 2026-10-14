using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Areas of raw primitives. Inputs are finite; radii and extents are nonnegative.</summary>
public static partial class Area2D
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

    /// <summary>The area covered by two ordered axis-aligned rectangles, counting overlap once.</summary>
    public static float RectangleUnion(Vector2 firstMin, Vector2 firstMax, Vector2 secondMin, Vector2 secondMax)
    {
        var width = Math.Max(0d, (double)Math.Min(firstMax.X, secondMax.X) - Math.Max(firstMin.X, secondMin.X));
        var height = Math.Max(0d, (double)Math.Min(firstMax.Y, secondMax.Y) - Math.Max(firstMin.Y, secondMin.Y));
        return (float)(((double)firstMax.X - firstMin.X) * ((double)firstMax.Y - firstMin.Y)
            + ((double)secondMax.X - secondMin.X) * ((double)secondMax.Y - secondMin.Y) - width * height);
    }

    /// <summary>The rectangle area minus the four square corners outside its circular arcs.</summary>
    /// <param name="min">The lower-left corner of the outer rectangle.</param>
    /// <param name="max">The upper-right corner of the outer rectangle.</param>
    /// <param name="radius">The corner radius, at most half the shorter side.</param>
    /// <returns>The area enclosed by the rounded rectangle.</returns>
    public static float RoundedRectangle(Vector2 min, Vector2 max, float radius) =>
        Rectangle(min, max) - (4f - MathF.PI) * radius * radius;

    /// <summary>The unsigned area of a simple polygon given in perimeter order, in either winding.</summary>
    /// <param name="vertices">The perimeter vertices without a repeated closing vertex.</param>
    /// <returns>The enclosed area.</returns>
    public static float Polygon(ReadOnlySpan<Vector2> vertices) => MathF.Abs(PolygonGeometry2D.SignedAreaTwice(vertices)) / 2f;
}
