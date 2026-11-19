using App2d.Core.Validation;

namespace App2d.Core.Geometry.Convex;

/// <summary>Raw geometry projected onto any finite axis, including non-unit and zero axes. A zero axis projects everything to zero.</summary>
public static class Projection2D
{
    /// <summary>Projects a set of vertices onto an axis.</summary>
    /// <param name="vertices">At least one vertex.</param>
    /// <param name="axis">The projection axis; length scales the result.</param>
    /// <param name="offset">A translation applied to every vertex before projecting, avoiding a transformed copy.</param>
    /// <returns>The [min, max] range of the dot products.</returns>
    public static Interval<float> Polygon(ReadOnlySpan<Vector2f> vertices, Vector2f axis, Vector2f offset = default)
    {
        Ensure.ThrowIfTooShort(vertices, 1);
        var min = Vector2f.Dot(vertices[0] + offset, axis);
        var max = min;
        foreach (var point in vertices[1..])
        {
            var value = Vector2f.Dot(point + offset, axis);
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
    public static Interval<float> Polygon(IReadOnlyList<Vector2f> vertices, Vector2f axis, Vector2f offset = default)
    {
        Ensure.ThrowIfNull(vertices);
        Ensure.ThrowIf(vertices.Count == 0, "At least one vertex is required.", nameof(vertices));
        var min = Vector2f.Dot(vertices[0] + offset, axis);
        var max = min;
        for (var i = 1; i < vertices.Count; i++)
        {
            var value = Vector2f.Dot(vertices[i] + offset, axis);
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
    public static Interval<float> Circle(Vector2f center, float radius, Vector2f axis)
    {
        var centerProjection = Vector2f.Dot(center, axis);
        var extent = radius * axis.Length();
        return new(centerProjection - extent, centerProjection + extent);
    }

    /// <summary>Projects an axis-aligned ellipse onto an axis exactly.</summary>
    /// <param name="center">The ellipse center.</param>
    /// <param name="radii">The half-extents along X and Y.</param>
    /// <param name="axis">The projection axis; length scales the result.</param>
    /// <returns>The center projection extended by the length of radii * axis on both sides.</returns>
    public static Interval<float> Ellipse(Vector2f center, Vector2f radii, Vector2f axis)
    {
        var centerProjection = Vector2f.Dot(center, axis);
        var extent = (radii * axis).Length();
        return new(centerProjection - extent, centerProjection + extent);
    }

    /// <summary>Projects a capsule onto an axis.</summary>
    /// <param name="start">One spine endpoint.</param>
    /// <param name="end">The other spine endpoint.</param>
    /// <param name="radius">The capsule radius.</param>
    /// <param name="axis">The projection axis; length scales the result.</param>
    /// <returns>The spine projection extended by radius * axis length on both sides.</returns>
    public static Interval<float> Capsule(Vector2f start, Vector2f end, float radius, Vector2f axis)
    {
        var a = Vector2f.Dot(start, axis);
        var b = Vector2f.Dot(end, axis);
        var extent = radius * axis.Length();
        return new(Math.Min(a, b) - extent, Math.Max(a, b) + extent);
    }
}
