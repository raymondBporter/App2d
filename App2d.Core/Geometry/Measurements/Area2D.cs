using App2d.Core.Geometry.Operations.Polygons;

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
    public static float Ellipse(Vector2f radii) => MathF.PI * radii.X * radii.Y;

    /// <summary>The area of a capsule: a rectangle around the spine plus a full circle of end caps.</summary>
    /// <param name="start">One spine endpoint.</param>
    /// <param name="end">The other spine endpoint.</param>
    /// <param name="radius">The capsule radius.</param>
    /// <returns>Twice the radius times the spine length, plus the circle area.</returns>
    public static float Capsule(Vector2f start, Vector2f end, float radius) => 2f * radius * Vector2f.Distance(start, end) + Circle(radius);

    /// <summary>The area of an ordered axis-aligned rectangle.</summary>
    /// <param name="min">The lower-left corner.</param>
    /// <param name="max">The upper-right corner.</param>
    /// <returns>Width times height.</returns>
    public static float Rectangle(Vector2f min, Vector2f max) => (max.X - min.X) * (max.Y - min.Y);

    /// <summary>The area covered by two ordered axis-aligned rectangles, counting overlap once.</summary>
    public static float RectangleUnion(Vector2f firstMin, Vector2f firstMax, Vector2f secondMin, Vector2f secondMax)
    {
        return (float)(((double)firstMax.X - firstMin.X) * ((double)firstMax.Y - firstMin.Y)
            + ((double)secondMax.X - secondMin.X) * ((double)secondMax.Y - secondMin.Y) - RectangleOverlap(firstMin, firstMax, secondMin, secondMax));
    }

    /// <summary>The area two ordered axis-aligned rectangles share; zero when they only touch or lie apart.</summary>
    public static float RectangleIntersection(Vector2f firstMin, Vector2f firstMax, Vector2f secondMin, Vector2f secondMax) => (float)RectangleOverlap(firstMin, firstMax, secondMin, secondMax);

    private static double RectangleOverlap(Vector2f firstMin, Vector2f firstMax, Vector2f secondMin, Vector2f secondMax)
    {
        var width = Math.Max(0d, (double)Math.Min(firstMax.X, secondMax.X) - Math.Max(firstMin.X, secondMin.X));
        var height = Math.Max(0d, (double)Math.Min(firstMax.Y, secondMax.Y) - Math.Max(firstMin.Y, secondMin.Y));
        return width * height;
    }

    /// <summary>The rectangle area minus the four square corners outside its circular arcs.</summary>
    /// <param name="min">The lower-left corner of the outer rectangle.</param>
    /// <param name="max">The upper-right corner of the outer rectangle.</param>
    /// <param name="radius">The corner radius, at most half the shorter side.</param>
    /// <returns>The area enclosed by the rounded rectangle.</returns>
    public static float RoundedRectangle(Vector2f min, Vector2f max, float radius) =>
        Rectangle(min, max) - (4f - MathF.PI) * radius * radius;

    /// <summary>The unsigned area of a simple polygon given in perimeter order, in either winding.</summary>
    /// <param name="vertices">The perimeter vertices without a repeated closing vertex.</param>
    /// <returns>The enclosed area.</returns>
    public static float Polygon(ReadOnlySpan<Vector2f> vertices) => MathF.Abs(PolygonGeometry2D.SignedAreaTwice(vertices)) / 2f;
}
