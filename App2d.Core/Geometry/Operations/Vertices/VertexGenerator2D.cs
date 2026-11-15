using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Operations.Vertices;

/// <summary>
/// Writes local-space perimeters into caller-owned buffers. No shapes, cached bounds, graphics types or allocations.
/// Closed contours omit a repeated closing vertex. Angles are radians; increasing angles are counter-clockwise in Y-up coordinates.
/// </summary>
public static class VertexGenerator2D
{
    /// <summary>Divisible by the common 12, 24, 48, 60, 80 and 120 segment counts.</summary>
    public const int UnitCircleSegments = 240;
    private static readonly Vector2f[] CircleSamples = CreateUnitCircle();

    /// <summary>
    /// Shared unit-circle directions, including the closing sample at 2π. A resolution that divides
    /// <see cref="UnitCircleSegments"/> samples this span at an exact stride. For other resolutions,
    /// calculate directions directly instead of rounding the stride.
    /// </summary>
    public static ReadOnlySpan<Vector2f> UnitCircle => CircleSamples;

    private static Vector2f[] CreateUnitCircle()
    {
        var samples = new Vector2f[UnitCircleSegments + 1];
        for (var i = 0; i <= UnitCircleSegments; i++)
            samples[i] = PointOnEllipse(Vector2f.Zero, Vector2f.One, i / (float)UnitCircleSegments * MathF.Tau);
        return samples;
    }

    /// <summary>A point on an axis-aligned ellipse at a parametric angle.</summary>
    /// <param name="center">The ellipse center.</param>
    /// <param name="radii">The half-extents along X and Y.</param>
    /// <param name="angle">The parametric angle in radians; zero is the +X extreme.</param>
    /// <returns>Center + (cos, sin) scaled by the radii.</returns>
    public static Vector2f  PointOnEllipse(Vector2f center, Vector2f radii, float angle) => center + Vector2f.Direction(angle) * radii;

    /// <summary>Writes a full ellipse; the buffer length selects the number of segments, starting on local +X.</summary>
    /// <param name="vertices">A buffer of at least three entries; every entry is written.</param>
    /// <param name="center">The finite center.</param>
    /// <param name="radii">Finite, nonnegative half-extents.</param>
    /// <returns>The number of vertices written, equal to the buffer length.</returns>
    public static int WriteEllipse(Span<Vector2f> vertices, Vector2f center, Vector2f radii)
    {
        Ensure.ThrowIfTooShort(vertices, 3);
        Ensure.Finite(center);
        Ensure.NonNegative(radii);
        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] = PointOnEllipse(center, radii, i * MathF.Tau / vertices.Length);
        }

        return vertices.Length;
    }

    /// <summary>Writes a full circle; the buffer length selects the number of segments, starting on local +X.</summary>
    /// <param name="vertices">A buffer of at least three entries; every entry is written.</param>
    /// <param name="center">The finite center.</param>
    /// <param name="radius">The finite, nonnegative radius.</param>
    /// <returns>The number of vertices written, equal to the buffer length.</returns>
    public static int WriteCircle(Span<Vector2f> vertices, Vector2f center, float radius) => WriteEllipse(vertices, center, new(radius));

    /// <summary>
    /// Writes six counter-clockwise corners of a regular hexagon. At zero angle the first corner points along +X.
    /// Radius is the center-to-corner distance. Entries after the sixth are untouched.
    /// </summary>
    public static int WriteRegularHexagon(Span<Vector2f> vertices, Vector2f center, float radius, float angleRadians = 0f)
    {
        Ensure.ThrowIfTooShort(vertices, 6);
        Ensure.Finite(center);
        Ensure.Positive(radius);

        const double sin60 = 0.8660254037844386d;
        var origin = center.ToVector2d();
        var axis = Vector2d.Direction(angleRadians) * radius;
        var halfAxis = axis / 2d;
        var side = axis.PerpCcw * sin60;
        // The other two pairs use cos(60°) = 1/2 and sin(60°), expressed in the rotated basis.
        var upperRight = halfAxis + side;
        var upperLeft = side - halfAxis;
        vertices[0] = (origin + axis).ToVector2f();
        vertices[1] = (origin + upperRight).ToVector2f();
        vertices[2] = (origin + upperLeft).ToVector2f();
        vertices[3] = (origin - axis).ToVector2f();
        vertices[4] = (origin - upperRight).ToVector2f();
        vertices[5] = (origin - upperLeft).ToVector2f();
        return 6;
    }

    /// <summary>Writes an elliptical arc including both endpoints; the buffer length selects the sample count.</summary>
    /// <param name="vertices">A buffer of at least two entries; every entry is written.</param>
    /// <param name="center">The finite center.</param>
    /// <param name="radii">Finite, nonnegative half-extents.</param>
    /// <param name="startAngle">The finite parametric start angle in radians.</param>
    /// <param name="sweepAngle">The finite signed sweep in radians; negative sweeps run clockwise.</param>
    /// <returns>The number of vertices written, equal to the buffer length.</returns>
    public static int WriteArc(Span<Vector2f> vertices, Vector2f center, Vector2f radii, float startAngle, float sweepAngle)
    {
        Ensure.ThrowIfTooShort(vertices, 2);
        Ensure.Finite(center);
        Ensure.NonNegative(radii);
        Ensure.Finite(startAngle);
        Ensure.Finite(sweepAngle);
        for (var i = 0; i < vertices.Length; i++)
        {
            vertices[i] = PointOnEllipse(center, radii, startAngle + i * sweepAngle / (vertices.Length - 1));
        }

        return vertices.Length;
    }

    /// <summary>Writes the four corners of a rectangle counter-clockwise from min. Extra buffer entries are untouched.</summary>
    /// <param name="vertices">A buffer of at least four entries.</param>
    /// <param name="min">The finite lower-left corner.</param>
    /// <param name="max">The finite upper-right corner, component-wise at least <paramref name="min"/>.</param>
    /// <returns>Four.</returns>
    public static int WriteRectangle(Span<Vector2f> vertices, Vector2f min, Vector2f max)
    {
        Ensure.ThrowIfTooShort(vertices, 4);
        Ensure.GreaterThanOrEqual(max, min);
        vertices[0] = min;
        vertices[1] = new(max.X, min.Y);
        vertices[2] = max;
        vertices[3] = new(min.X, max.Y);
        return 4;
    }

    /// <summary>
    /// Writes a rounded rectangle starting on the right edge of the upper-right corner. Writes 4 * (segmentsPerCorner + 1)
    /// vertices; each quarter arc includes its endpoints. The radius clamps to half the shorter side, and a zero radius
    /// deliberately retains repeated corner samples so the vertex count stays fixed.
    /// </summary>
    /// <param name="vertices">A buffer of at least 4 * (segmentsPerCorner + 1) entries.</param>
    /// <param name="min">The finite lower-left corner.</param>
    /// <param name="max">The finite upper-right corner, component-wise at least <paramref name="min"/>.</param>
    /// <param name="radius">The finite, nonnegative corner radius.</param>
    /// <param name="segmentsPerCorner">The positive number of segments per quarter arc.</param>
    /// <returns>The number of vertices written.</returns>
    public static int WriteRoundedRectangle(Span<Vector2f> vertices, Vector2f min, Vector2f max, float radius, int segmentsPerCorner = 8)
    {
        Ensure.Positive(segmentsPerCorner);
        var count = checked(4 * (segmentsPerCorner + 1));
        Ensure.ThrowIfTooShort(vertices, count);
        Ensure.GreaterThanOrEqual(max, min);
        Ensure.NonNegative(radius);
        radius = Math.Min(radius, Math.Min(max.X - min.X, max.Y - min.Y) * 0.5f);
        for (var corner = 0; corner < 4; corner++)
        {
            var center = new Vector2f(corner is 0 or 3 ? max.X - radius : min.X + radius, corner < 2 ? max.Y - radius : min.Y + radius);
            WriteArc(vertices.Slice(corner * (segmentsPerCorner + 1), segmentsPerCorner + 1), center, new(radius), corner * MathF.PI / 2f, MathF.PI / 2f);
        }
        return count;
    }

    /// <summary>Writes a capsule as two inclusive semicircles, starting on the clockwise side of the end cap.</summary>
    /// <param name="vertices">A buffer of at least 2 * (segmentsPerCap + 1) entries.</param>
    /// <param name="start">The finite spine start.</param>
    /// <param name="end">The finite spine end.</param>
    /// <param name="radius">The finite, nonnegative radius.</param>
    /// <param name="segmentsPerCap">The positive number of segments per semicircle.</param>
    /// <returns>The number of vertices written.</returns>
    public static int WriteCapsule(Span<Vector2f> vertices, Vector2f start, Vector2f end, float radius, int segmentsPerCap)
    {
        Ensure.Positive(segmentsPerCap);
        var capCount = checked(segmentsPerCap + 1);
        var count = checked(2 * capCount);
        Ensure.ThrowIfTooShort(vertices, count);
        Ensure.Finite(start);
        Ensure.Finite(end);
        Ensure.NonNegative(radius);
        var angle = (end - start).AngleRadians;
        WriteArc(vertices[..capCount], end, new(radius), angle - MathF.PI / 2f, MathF.PI);
        WriteArc(vertices.Slice(capCount, capCount), start, new(radius), angle + MathF.PI / 2f, MathF.PI);
        return count;
    }
}
