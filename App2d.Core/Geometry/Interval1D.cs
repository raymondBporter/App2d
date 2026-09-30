using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>A [Min, Max] projection of a shape onto an axis.</summary>
public readonly record struct Interval1D(float Min, float Max)
{
    public static Interval1D ProjectPolygon(ReadOnlySpan<Vector2> vertices, Vector2 axis) =>
        Projection2D.Polygon(vertices, axis);

    public static Interval1D ProjectCapsule(Vector2 start, Vector2 end, float radius, Vector2 axis) =>
        Projection2D.Capsule(start, end, radius, axis);
}
