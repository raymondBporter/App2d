using App2d.Core.Geometry;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A rectangle with circular corners: its inset rectangle expanded by <see cref="Radius"/>.</summary>
public sealed class RoundedRectangle2D : IConvexShape2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.RoundedRectangle;

    public RoundedRectangle2D(Vector2 min, Vector2 max, float radius)
    {
        ArgGuard.ThrowIfNotFiniteOrNotComponentWiseLessThan(min, max);
        ArgGuard.ThrowIfNotFiniteOrNegative(radius);
        if (radius > MathF.Min(max.X - min.X, max.Y - min.Y) / 2f)
            throw new ArgumentOutOfRangeException(nameof(radius), "Corner radius cannot exceed half the shorter side.");
        Min = min;
        Max = max;
        Radius = radius;
    }

    public Vector2 Min { get; }
    public Vector2 Max { get; }
    public float Radius { get; }
    public Vector2 CoreMin => Min + new Vector2(Radius);
    public Vector2 CoreMax => Max - new Vector2(Radius);
    public float Area => Area2D.RoundedRectangle(Min, Max, Radius);
    public bool ContainsPoint(Vector2 point) =>
        Distance2D.SignedDistanceToRectangle(point, CoreMin, CoreMax) <= Radius;
    public Vector2 GetSupportPoint(Vector2 direction) =>
        SupportPoint2D.Circle(direction, SupportPoint2D.Rectangle(direction, CoreMin, CoreMax), Radius);

    public static RoundedRectangle2D FromSize(Vector2 size, float radius, Vector2 center = default)
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(size);
        ArgGuard.ThrowIfNotFinite(center);
        return new RoundedRectangle2D(center - size / 2f, center + size / 2f, radius);
    }
}
