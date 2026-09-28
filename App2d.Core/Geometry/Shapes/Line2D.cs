using App2d.Core.Geometry.Functions;
using System.Numerics;

namespace App2d.Core.Geometry.Shapes;

/// <summary>
/// An infinite line through Origin in both directions along a unit Direction.
/// Construct explicitly: default has no direction and is rejected by queries.
/// </summary>
public readonly record struct Line2D : IGeometry2D
{
    public Line2D(Vector2 origin, Vector2 direction)
    {
        ArgGuard.ThrowIfNotFinite(origin);
        Origin = origin;
        Direction = LinearGeometry2D.NormalizeDirection(direction);
    }

    public Vector2 Origin { get; }
    public Vector2 Direction { get; }
    public bool IsValid => Direction != Vector2.Zero;

    /// <summary>Signed distance along the line; negative distances run opposite Direction.</summary>
    public Vector2 GetPoint(float distance)
    {
        Validate();
        ArgGuard.ThrowIfNotFinite(distance);
        return Origin + Direction * distance;
    }

    public Vector2 ClosestPoint(Vector2 point) => ClosestPoint2D.OnLine(point, Origin, Direction);
    public float DistanceTo(Vector2 point) => Distance2D.Distance(point, this);

    /// <summary>Uses a perpendicular distance tolerance of 0.00001 input units.</summary>
    public bool ContainsPoint(Vector2 point) => ContainsPoint(point, LinearGeometry2D.DefaultPointTolerance);

    /// <summary>Zero tolerance requests exact containment; tolerance is measured in input units.</summary>
    public bool ContainsPoint(Vector2 point, float tolerance)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(tolerance);
        return LinearGeometry2D.Distance(point, Origin, Direction, forwardOnly: false) <= tolerance;
    }

    internal void Validate() => ArgGuard.ThrowIfNotFiniteOrZero(Direction);

    public static Line2D FromPoints(Vector2 first, Vector2 second) => new(first, second - first);
}
