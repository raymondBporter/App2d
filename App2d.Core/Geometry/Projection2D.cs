using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Raw geometry projected onto any finite axis, including non-unit and zero axes. A zero axis projects everything to zero.</summary>
public static class Projection2D
{
    /// <summary>Projects a set of vertices onto an axis.</summary>
    /// <param name="vertices">At least one vertex.</param>
    /// <param name="axis">The projection axis; length scales the result.</param>
    /// <param name="offset">A translation applied to every vertex before projecting, avoiding a transformed copy.</param>
    /// <returns>The [min, max] range of the dot products.</returns>
    public static Interval1D Polygon(ReadOnlySpan<Vector2> vertices, Vector2 axis, Vector2 offset = default)
    {
        ArgGuard.ThrowIfTooShort(vertices, 1);
        var min = Vector2.Dot(vertices[0] + offset, axis);
        var max = min;
        foreach (var point in vertices[1..])
        {
            var value = Vector2.Dot(point + offset, axis);
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }
        return new(min, max);
    }

    /// <summary>List adapter for authored geometry; it does not copy or allocate an intermediate vertex array.</summary>
    /// <param name="vertices">At least one vertex.</param>
    /// <param name="axis">The projection axis; length scales the result.</param>
    /// <param name="offset">A translation applied to every vertex before projecting.</param>
    /// <returns>The [min, max] range of the dot products.</returns>
    public static Interval1D Polygon(IReadOnlyList<Vector2> vertices, Vector2 axis, Vector2 offset = default)
    {
        ArgGuard.ThrowIfNull(vertices);
        ArgGuard.ThrowIf(vertices.Count == 0, "At least one vertex is required.", nameof(vertices));
        var min = Vector2.Dot(vertices[0] + offset, axis);
        var max = min;
        for (var i = 1; i < vertices.Count; i++)
        {
            var value = Vector2.Dot(vertices[i] + offset, axis);
            min = Math.Min(min, value);
            max = Math.Max(max, value);
        }
        return new(min, max);
    }

    /// <summary>Projects a circle onto an axis.</summary>
    /// <param name="center">The circle center.</param>
    /// <param name="radius">The circle radius.</param>
    /// <param name="axis">The projection axis; length scales the result.</param>
    /// <returns>The center projection extended by radius * axis length on both sides.</returns>
    public static Interval1D Circle(Vector2 center, float radius, Vector2 axis)
    {
        var centerProjection = Vector2.Dot(center, axis);
        var extent = radius * axis.Length();
        return new(centerProjection - extent, centerProjection + extent);
    }

    /// <summary>Projects an axis-aligned ellipse onto an axis exactly.</summary>
    /// <param name="center">The ellipse center.</param>
    /// <param name="radii">The half-extents along X and Y.</param>
    /// <param name="axis">The projection axis; length scales the result.</param>
    /// <returns>The center projection extended by the length of radii * axis on both sides.</returns>
    public static Interval1D Ellipse(Vector2 center, Vector2 radii, Vector2 axis)
    {
        var centerProjection = Vector2.Dot(center, axis);
        var extent = (radii * axis).Length();
        return new(centerProjection - extent, centerProjection + extent);
    }

    /// <summary>Projects a capsule onto an axis.</summary>
    /// <param name="start">One spine endpoint.</param>
    /// <param name="end">The other spine endpoint.</param>
    /// <param name="radius">The capsule radius.</param>
    /// <param name="axis">The projection axis; length scales the result.</param>
    /// <returns>The spine projection extended by radius * axis length on both sides.</returns>
    public static Interval1D Capsule(Vector2 start, Vector2 end, float radius, Vector2 axis)
    {
        var a = Vector2.Dot(start, axis);
        var b = Vector2.Dot(end, axis);
        var extent = radius * axis.Length();
        return new(Math.Min(a, b) - extent, Math.Max(a, b) + extent);
    }
}
