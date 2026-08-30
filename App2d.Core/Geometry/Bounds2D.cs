using System.Numerics;

namespace App2d.Engine.Geometry;

public readonly record struct Bounds2D(Vector2 Min, Vector2 Max)
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
        Left <= other.Right && Right >= other.Left &&
        Bottom <= other.Top && Top >= other.Bottom;

    public Bounds2D TransformedBy(Matrix3x2 transform)
    {
        // Infinite shapes such as half-spaces must stay broad-phase candidates.
        // Transforming infinities directly would produce NaNs for zero matrix terms.
        if (!IsFinite)
            return Unbounded;

        Span<Vector2> corners =
        [
            Vector2.Transform(Min, transform),
            Vector2.Transform(new Vector2(Max.X, Min.Y), transform),
            Vector2.Transform(Max, transform),
            Vector2.Transform(new Vector2(Min.X, Max.Y), transform),
        ];
        return FromPoints(corners);
    }

    public static Bounds2D FromPoints(ReadOnlySpan<Vector2> points)
    {
        ArgGuard.ThrowIfTooShort(points, 1);

        var min = points[0];
        var max = points[0];
        foreach (var point in points[1..])
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        return new Bounds2D(min, max);
    }
}
