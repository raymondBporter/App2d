using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>
/// An origin and a unit direction, extending forward without limit. Distances are in input units.
/// Construct explicitly: the default value has no direction and is rejected by every query.
/// </summary>
public readonly record struct Ray2D : IGeometry2D
{
    /// <summary>Creates a ray from an origin and any finite, nonzero direction; the direction is normalized.</summary>
    /// <param name="origin">The finite start of the ray.</param>
    /// <param name="direction">A finite, nonzero direction; length is ignored.</param>
    public Ray2D(Vector2 origin, Vector2 direction) : this(origin, new Direction2D(direction)) { }

    private Ray2D(Vector2 origin, Direction2D direction)
    {
        ArgGuard.ThrowIfNotFinite(origin);
        direction.Validate();
        Origin = origin;
        UnitDirection = direction;
    }

    /// <summary>The start of the ray.</summary>
    public Vector2 Origin { get; }

    /// <summary>The unit direction as a vector.</summary>
    public Vector2 Direction => UnitDirection.Vector;

    /// <summary>The validated unit direction.</summary>
    public Direction2D UnitDirection { get; }

    /// <summary>False for the default value, which has no direction.</summary>
    public bool IsValid => UnitDirection.IsValid;

    /// <summary>Returns the point at a distance along the ray.</summary>
    /// <param name="t">A finite, nonnegative distance from the origin.</param>
    /// <returns>Origin + Direction * t.</returns>
    public Vector2 PointAt(float t)
    {
        Validate();
        ArgGuard.ThrowIfNegativeOrNotFinite(t);
        return Origin + Direction * t;
    }

    /// <summary>Alias for <see cref="PointAt"/>.</summary>
    /// <param name="distance">A finite, nonnegative distance from the origin.</param>
    /// <returns>The point at that distance.</returns>
    public Vector2 GetPoint(float distance) => PointAt(distance);

    /// <summary>The closest point on the ray to a point, clamped at the origin.</summary>
    /// <param name="point">The finite query point.</param>
    /// <returns>The nearest point on the forward ray.</returns>
    public Vector2 ClosestPoint(Vector2 point) => ClosestPoint2D.OnRay(point, Origin, Direction);

    /// <summary>The distance from a point to the ray, measured to the origin for points behind it.</summary>
    /// <param name="point">The finite query point.</param>
    /// <returns>An unsigned distance in input units.</returns>
    public float DistanceTo(Vector2 point) => Distance2D.Distance(point, this);

    /// <summary>Tests containment with a distance tolerance of 0.00001 input units, including around the origin.</summary>
    /// <param name="point">The finite point to test.</param>
    /// <returns>True when the point lies within the default tolerance of the ray.</returns>
    public bool ContainsPoint(Vector2 point) => ContainsPoint(point, LinearGeometry2D.DefaultPointTolerance);

    /// <summary>Tests containment with an explicit tolerance; zero requests exact containment.</summary>
    /// <param name="point">The finite point to test.</param>
    /// <param name="tolerance">A nonnegative distance in input units.</param>
    /// <returns>True when the point lies within the tolerance of the ray.</returns>
    public bool ContainsPoint(Vector2 point, float tolerance)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(tolerance);
        return LinearGeometry2D.Distance(point, Origin, Direction, forwardOnly: true) <= tolerance;
    }

    internal void Validate() => UnitDirection.Validate();

    /// <summary>Creates a ray from an origin and an already-normalized direction.</summary>
    /// <param name="origin">The finite start of the ray.</param>
    /// <param name="direction">A valid unit direction.</param>
    /// <returns>The ray from the origin along the direction.</returns>
    public static Ray2D FromDirection(Vector2 origin, Direction2D direction) => new(origin, direction);

    /// <summary>Creates the ray from one finite point toward another, distinct point.</summary>
    /// <param name="origin">The start of the ray.</param>
    /// <param name="target">A distinct point the ray passes through.</param>
    /// <returns>The ray from the origin through the target.</returns>
    public static Ray2D FromPoints(Vector2 origin, Vector2 target) => FromDirection(origin, Direction2D.FromPoints(origin, target));
}
