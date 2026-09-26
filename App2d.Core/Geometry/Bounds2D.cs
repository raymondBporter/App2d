using System.Numerics;

namespace App2d.Core.Geometry;

public readonly record struct Bounds2D(Vector2 Min, Vector2 Max) : IRect2D
{
    public static Bounds2D Unbounded { get; } = new(new Vector2(float.NegativeInfinity), new Vector2(float.PositiveInfinity));

    public Vector2 Center => (Min + Max) / 2f;
    public Vector2 Size => Max - Min;
    public float Left => Min.X;
    public float Right => Max.X;
    public float Bottom => Min.Y;
    public float Top => Max.Y;
    public bool IsFinite =>
        float.IsFinite(Min.X) && float.IsFinite(Min.Y) &&
        float.IsFinite(Max.X) && float.IsFinite(Max.Y);

    public bool Intersects(Bounds2D other) =>
        PrimitiveGeometry2D.RectanglesIntersect(Min, Max, other.Min, other.Max);

    public Bounds2D TransformedBy(Matrix3x2 transform) => BoundsGeometry2D.Transform(this, transform);

    public static Bounds2D FromPoints(ReadOnlySpan<Vector2> points) => BoundsGeometry2D.FromPoints(points);
}
