using System.Numerics;

namespace App2d.Engine.Geometry;

/// <summary>A [Min, Max] projection of a shape onto an axis.</summary>
public readonly record struct Interval1D(float Min, float Max)
{
    public static Interval1D ProjectPolygon(ReadOnlySpan<Vector2> vertices, Vector2 axis)
    {
        var min = Vector2.Dot(vertices[0], axis);
        var max = min;
        foreach (var vertex in vertices[1..])
        {
            var projection = Vector2.Dot(vertex, axis);
            min = Math.Min(min, projection);
            max = Math.Max(max, projection);
        }

        return new Interval1D(min, max);
    }

    public static Interval1D ProjectCapsule(Vector2 start, Vector2 end, float radius, Vector2 axis)
    {
        var startProjection = Vector2.Dot(start, axis);
        var endProjection = Vector2.Dot(end, axis);
        return new Interval1D(Math.Min(startProjection, endProjection) - radius, Math.Max(startProjection, endProjection) + radius);
    }
}
