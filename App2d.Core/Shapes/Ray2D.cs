using App2d.Core.Validation;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Functions;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>
/// An origin and a unit direction, extending forward without limit. Distances are in input units.
/// Construct explicitly: default has no direction and is rejected by queries.
/// </summary>
public readonly record struct Ray2D : IGeometry2D
{

    public Ray2D(Vector2 origin, Vector2 direction) : this(origin, new Direction2D(direction)) { }

    private Ray2D(Vector2 origin, Direction2D direction)
    {
        ArgGuard.ThrowIfNotFinite(origin);
        direction.Validate();
        Origin = origin;
        UnitDirection = direction;
    }

    public Vector2 Origin { get; }
    public Vector2 Direction => UnitDirection.Vector;
    public Direction2D UnitDirection { get; }
    public bool IsValid => UnitDirection.IsValid;

    /// <summary>Returns a point on the ray; t must be finite and nonnegative.</summary>
    public Vector2 PointAt(float t)
    {
        Validate();
        ArgGuard.ThrowIfNegativeOrNotFinite(t);
        return Origin + Direction * t;
    }

    public Vector2 GetPoint(float distance) => PointAt(distance);

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

    internal void Validate() => UnitDirection.Validate();

    public static Ray2D FromDirection(Vector2 origin, Direction2D direction) => new(origin, direction);

    public static Ray2D FromPoints(Vector2 origin, Vector2 target) =>
        FromDirection(origin, Direction2D.FromPoints(origin, target));
}
