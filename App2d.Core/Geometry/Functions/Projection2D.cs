using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>Raw geometry projected onto any finite axis, including non-unit and zero axes.</summary>
public static class Projection2D
{
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

    /// <summary>List adapter for authored geometry; does not copy or allocate an intermediate vertex array.</summary>
    public static Interval1D Polygon(IReadOnlyList<Vector2> vertices, Vector2 axis, Vector2 offset = default)
    {
        ArgGuard.ThrowIfNull(vertices);
        if (vertices.Count == 0) throw new ArgumentException("At least one vertex is required.", nameof(vertices));
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

    public static Interval1D Circle(Vector2 center, float radius, Vector2 axis)
    {
        var centerProjection = Vector2.Dot(center, axis);
        var extent = radius * axis.Length();
        return new(centerProjection - extent, centerProjection + extent);
    }

    public static Interval1D Capsule(Vector2 start, Vector2 end, float radius, Vector2 axis)
    {
        var a = Vector2.Dot(start, axis);
        var b = Vector2.Dot(end, axis);
        var extent = radius * axis.Length();
        return new(Math.Min(a, b) - extent, Math.Max(a, b) + extent);
    }
}
