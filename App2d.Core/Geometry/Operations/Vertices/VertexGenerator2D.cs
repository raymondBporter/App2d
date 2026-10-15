using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>
/// Writes local-space perimeters into caller-owned buffers. No shapes, cached bounds, graphics types or allocations.
/// Closed contours omit a repeated closing vertex. Angles are radians; increasing angles are counter-clockwise in Y-up coordinates.
/// </summary>
public static class VertexGenerator2D
{
    /// <summary>A point on an axis-aligned ellipse at a parametric angle.</summary>
    /// <param name="center">The ellipse center.</param>
    /// <param name="radii">The half-extents along X and Y.</param>
    /// <param name="angle">The parametric angle in radians; zero is the +X extreme.</param>
    /// <returns>Center + (cos, sin) scaled by the radii.</returns>
    public static Vector2 PointOnEllipse(Vector2 center, Vector2 radii, float angle) => center + Polar2D.Direction(angle) * radii;

    /// <summary>Writes a full ellipse; the buffer length selects the number of segments, starting on local +X.</summary>
    /// <param name="vertices">A buffer of at least three entries; every entry is written.</param>
    /// <param name="center">The finite center.</param>
    /// <param name="radii">Finite, nonnegative half-extents.</param>
    /// <returns>The number of vertices written, equal to the buffer length.</returns>
    public static int WriteEllipse(Span<Vector2> vertices, Vector2 center, Vector2 radii)
    {
        ArgGuard.ThrowIfTooShort(vertices, 3);
        ArgGuard.ThrowIfNotFinite(center);
        ArgGuard.ThrowIfNotFiniteOrNegative(radii);
        for (var i = 0; i < vertices.Length; i++) vertices[i] = PointOnEllipse(center, radii, i * MathF.Tau / vertices.Length);
        return vertices.Length;
    }

    /// <summary>Writes a full circle; the buffer length selects the number of segments, starting on local +X.</summary>
    /// <param name="vertices">A buffer of at least three entries; every entry is written.</param>
    /// <param name="center">The finite center.</param>
    /// <param name="radius">The finite, nonnegative radius.</param>
    /// <returns>The number of vertices written, equal to the buffer length.</returns>
    public static int WriteCircle(Span<Vector2> vertices, Vector2 center, float radius) => WriteEllipse(vertices, center, new(radius));

    /// <summary>Writes an elliptical arc including both endpoints; the buffer length selects the sample count.</summary>
    /// <param name="vertices">A buffer of at least two entries; every entry is written.</param>
    /// <param name="center">The finite center.</param>
    /// <param name="radii">Finite, nonnegative half-extents.</param>
    /// <param name="startAngle">The finite parametric start angle in radians.</param>
    /// <param name="sweepAngle">The finite signed sweep in radians; negative sweeps run clockwise.</param>
    /// <returns>The number of vertices written, equal to the buffer length.</returns>
    public static int WriteArc(Span<Vector2> vertices, Vector2 center, Vector2 radii, float startAngle, float sweepAngle)
    {
        ArgGuard.ThrowIfTooShort(vertices, 2);
        ArgGuard.ThrowIfNotFinite(center);
        ArgGuard.ThrowIfNotFiniteOrNegative(radii);
        ArgGuard.ThrowIfNotFinite(startAngle);
        ArgGuard.ThrowIfNotFinite(sweepAngle);
        for (var i = 0; i < vertices.Length; i++) vertices[i] = PointOnEllipse(center, radii, startAngle + i * sweepAngle / (vertices.Length - 1));
        return vertices.Length;
    }

    /// <summary>Writes the four corners of a rectangle counter-clockwise from min. Extra buffer entries are untouched.</summary>
    /// <param name="vertices">A buffer of at least four entries.</param>
    /// <param name="min">The finite lower-left corner.</param>
    /// <param name="max">The finite upper-right corner, component-wise at least <paramref name="min"/>.</param>
    /// <returns>Four.</returns>
    public static int WriteRectangle(Span<Vector2> vertices, Vector2 min, Vector2 max)
    {
        ArgGuard.ThrowIfTooShort(vertices, 4);
        ArgGuard.ThrowIfNotFiniteOrNotComponentWiseGreaterThanOrEqual(max, min);
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
    public static int WriteRoundedRectangle(Span<Vector2> vertices, Vector2 min, Vector2 max, float radius, int segmentsPerCorner = 8)
    {
        ArgGuard.ThrowIfNotPositive(segmentsPerCorner);
        var count = checked(4 * (segmentsPerCorner + 1));
        ArgGuard.ThrowIfTooShort(vertices, count);
        ArgGuard.ThrowIfNotFiniteOrNotComponentWiseGreaterThanOrEqual(max, min);
        ArgGuard.ThrowIfNotFiniteOrNegative(radius);
        radius = Math.Min(radius, Math.Min(max.X - min.X, max.Y - min.Y) * 0.5f);
        for (var corner = 0; corner < 4; corner++)
        {
            var center = new Vector2(corner is 0 or 3 ? max.X - radius : min.X + radius, corner < 2 ? max.Y - radius : min.Y + radius);
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
    public static int WriteCapsule(Span<Vector2> vertices, Vector2 start, Vector2 end, float radius, int segmentsPerCap)
    {
        ArgGuard.ThrowIfNotPositive(segmentsPerCap);
        var capCount = checked(segmentsPerCap + 1);
        var count = checked(2 * capCount);
        ArgGuard.ThrowIfTooShort(vertices, count);
        ArgGuard.ThrowIfNotFinite(start);
        ArgGuard.ThrowIfNotFinite(end);
        ArgGuard.ThrowIfNotFiniteOrNegative(radius);
        var angle = (end - start).AngleRadians;
        WriteArc(vertices[..capCount], end, new(radius), angle - MathF.PI / 2f, MathF.PI);
        WriteArc(vertices.Slice(capCount, capCount), start, new(radius), angle + MathF.PI / 2f, MathF.PI);
        return count;
    }
}
