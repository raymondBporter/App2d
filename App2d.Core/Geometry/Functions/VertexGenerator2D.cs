using System.Numerics;
using App2d.Core.Mathematics;

namespace App2d.Core.Geometry.Functions;

/// <summary>
/// Writes local-space perimeters into caller-owned buffers. No shapes, cached bounds,
/// graphics types, or allocations. Closed contours omit a repeated closing vertex.
/// Angles are radians; increasing angles are counter-clockwise in Y-up coordinates.
/// </summary>
public static class VertexGenerator2D
{
    public static Vector2 PointOnEllipse(Vector2 center, Vector2 radii, float angle) =>
        center + Polar2D.Direction(angle) * radii;

    /// <summary>The buffer length selects the number of segments, starting on local +X.</summary>
    public static int WriteEllipse(Span<Vector2> vertices, Vector2 center, Vector2 radii)
    {
        ArgGuard.ThrowIfTooShort(vertices, 3);
        ArgGuard.ThrowIfNotFinite(center);
        ArgGuard.ThrowIfNotFiniteOrNegative(radii);
        for (var i = 0; i < vertices.Length; i++)
            vertices[i] = PointOnEllipse(center, radii, i * MathF.Tau / vertices.Length);
        return vertices.Length;
    }

    public static int WriteCircle(Span<Vector2> vertices, Vector2 center, float radius) =>
        WriteEllipse(vertices, center, new(radius));

    /// <summary>Includes both arc endpoints. The buffer length must be at least two.</summary>
    public static int WriteArc(Span<Vector2> vertices, Vector2 center, Vector2 radii, float startAngle, float sweepAngle)
    {
        ArgGuard.ThrowIfTooShort(vertices, 2);
        ArgGuard.ThrowIfNotFinite(center);
        ArgGuard.ThrowIfNotFiniteOrNegative(radii);
        ArgGuard.ThrowIfNotFinite(startAngle);
        ArgGuard.ThrowIfNotFinite(sweepAngle);
        for (var i = 0; i < vertices.Length; i++)
            vertices[i] = PointOnEllipse(center, radii, startAngle + i * sweepAngle / (vertices.Length - 1));
        return vertices.Length;
    }

    /// <summary>Four corners counter-clockwise from min. Extra buffer entries are untouched.</summary>
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
    /// Starts on the right edge of the upper-right corner. Writes 4 * (segmentsPerCorner + 1)
    /// vertices; each quarter arc includes its endpoints. Radius clamps to half the shorter side.
    /// Zero radius deliberately retains repeated corner samples.
    /// </summary>
    public static int WriteRoundedRectangle(Span<Vector2> vertices, Vector2 min, Vector2 max,
        float radius, int segmentsPerCorner = 8)
    {
        ArgGuard.ThrowIfNotPositive(segmentsPerCorner);
        var count = checked(4 * (segmentsPerCorner + 1));
        ArgGuard.ThrowIfTooShort(vertices, count);
        ArgGuard.ThrowIfNotFiniteOrNotComponentWiseGreaterThanOrEqual(max, min);
        ArgGuard.ThrowIfNotFiniteOrNegative(radius);
        radius = Math.Min(radius, Math.Min(max.X - min.X, max.Y - min.Y) * 0.5f);
        for (var corner = 0; corner < 4; corner++)
        {
            var center = new Vector2(corner is 0 or 3 ? max.X - radius : min.X + radius,
                corner < 2 ? max.Y - radius : min.Y + radius);
            WriteArc(vertices.Slice(corner * (segmentsPerCorner + 1), segmentsPerCorner + 1),
                center, new(radius), corner * MathF.PI / 2f, MathF.PI / 2f);
        }
        return count;
    }

    /// <summary>Two inclusive semicircles, starting on the clockwise side of end.</summary>
    public static int WriteCapsule(Span<Vector2> vertices, Vector2 start, Vector2 end, float radius,
        int segmentsPerCap)
    {
        ArgGuard.ThrowIfNotPositive(segmentsPerCap);
        var capCount = checked(segmentsPerCap + 1);
        var count = checked(2 * capCount);
        ArgGuard.ThrowIfTooShort(vertices, count);
        ArgGuard.ThrowIfNotFinite(start);
        ArgGuard.ThrowIfNotFinite(end);
        ArgGuard.ThrowIfNotFiniteOrNegative(radius);
        var axis = end - start;
        var angle = axis.AngleRadians;
        WriteArc(vertices[..capCount], end, new(radius), angle - MathF.PI / 2f, MathF.PI);
        WriteArc(vertices.Slice(capCount, capCount), start, new(radius), angle + MathF.PI / 2f, MathF.PI);
        return count;
    }

}
