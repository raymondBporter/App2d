using App2d.Core.Geometry.Functions;
using System.Numerics;

namespace App2d.Core.Geometry.Shapes;

/// <summary>
/// An origin and a unit direction, extending forward without limit. Distances are in input units.
/// Construct explicitly: default has no direction and is rejected by queries.
/// </summary>
public readonly record struct Ray2D : IGeometry2D
{
    public Ray2D(Vector2 origin, Vector2 direction)
    {
        ArgGuard.ThrowIfNotFinite(origin);
        Origin = origin;
        Direction = LinearGeometry2D.NormalizeDirection(direction);
    }

    public Vector2 Origin { get; }
    public Vector2 Direction { get; }
    public bool IsValid => Direction != Vector2.Zero;

    /// <summary>Returns a point on the ray; distance must be finite and nonnegative.</summary>
    public Vector2 GetPoint(float distance)
    {
        Validate();
        ArgGuard.ThrowIfNegativeOrNotFinite(distance);
        return Origin + Direction * distance;
    }

    public Vector2 ClosestPoint(Vector2 point) => ClosestPoint2D.OnRay(point, Origin, Direction);
    public float DistanceTo(Vector2 point) => Distance2D.Distance(point, this);

    /// <summary>Uses a distance tolerance of 0.00001 input units, including around the origin.</summary>
    public bool ContainsPoint(Vector2 point) => ContainsPoint(point, LinearGeometry2D.DefaultPointTolerance);

    /// <summary>Zero tolerance requests exact containment; tolerance is measured in input units.</summary>
    public bool ContainsPoint(Vector2 point, float tolerance)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(tolerance);
        return LinearGeometry2D.Distance(point, Origin, Direction, forwardOnly: true) <= tolerance;
    }

    internal void Validate() => ArgGuard.ThrowIfNotFiniteOrZero(Direction);

    public static Ray2D FromPoints(Vector2 origin, Vector2 target) => new(origin, target - origin);
}
