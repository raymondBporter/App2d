using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>
/// An infinite line through <see cref="Origin"/> in both directions along a unit <see cref="Direction"/>.
/// Construct explicitly: the default value has no direction and is rejected by every query.
/// </summary>
public readonly record struct Line2D : IGeometry2D
{
    /// <summary>Creates a line from a point and any finite, nonzero direction; the direction is normalized.</summary>
    /// <param name="origin">A finite point on the line.</param>
    /// <param name="direction">A finite, nonzero direction; length is ignored.</param>
    public Line2D(Vector2 origin, Vector2 direction) : this(origin, new Direction2D(direction)) { }

    private Line2D(Vector2 origin, Direction2D direction)
    {
        ArgGuard.ThrowIfNotFinite(origin);
        direction.Validate();
        Origin = origin;
        UnitDirection = direction;
    }

    /// <summary>A point on the line.</summary>
    public Vector2 Origin { get; }

    /// <summary>The unit direction as a vector.</summary>
    public Vector2 Direction => UnitDirection.Vector;

    /// <summary>The validated unit direction.</summary>
    public Direction2D UnitDirection { get; }

    /// <summary>False for the default value, which has no direction.</summary>
    public bool IsValid => UnitDirection.IsValid;

    /// <summary>Returns the point at a signed distance along the line.</summary>
    /// <param name="t">A finite signed distance; negative values run opposite <see cref="Direction"/>.</param>
    /// <returns>Origin + Direction * t.</returns>
    public Vector2 PointAt(float t)
    {
        Validate();
        ArgGuard.ThrowIfNotFinite(t);
        return Origin + Direction * t;
    }

    /// <summary>Alias for <see cref="PointAt"/>.</summary>
    /// <param name="distance">A finite signed distance along the line.</param>
    /// <returns>The point at that distance.</returns>
    public Vector2 GetPoint(float distance) => PointAt(distance);

    /// <summary>Classifies which side of the line a point lies on.</summary>
    /// <param name="test">The finite point to classify.</param>
    /// <param name="tolerance">A nonnegative distance in input units within which the point counts as on the line.</param>
    /// <returns>+1 on the right (clockwise) side of <see cref="Direction"/>, -1 on the left, or 0 within tolerance of the line.</returns>
    public int WhichSide(Vector2 test, float tolerance = 0f)
    {
        Validate();
        ArgGuard.ThrowIfNotFinite(test);
        ArgGuard.ThrowIfNegativeOrNotFinite(tolerance);
        var x = (double)test.X - Origin.X;
        var y = (double)test.Y - Origin.Y;
        var determinant = x * Direction.Y - Direction.X * y;
        return determinant > tolerance ? 1 : determinant < -tolerance ? -1 : 0;
    }

    /// <summary>The closest point on the line to a point.</summary>
    /// <param name="point">The finite query point.</param>
    /// <returns>The perpendicular foot on the line.</returns>
    public Vector2 ClosestPoint(Vector2 point) => ClosestPoint2D.OnLine(point, Origin, Direction);

    /// <summary>The perpendicular distance from a point to the line.</summary>
    /// <param name="point">The finite query point.</param>
    /// <returns>An unsigned distance in input units.</returns>
    public float DistanceTo(Vector2 point) => Distance2D.Distance(point, this);

    /// <summary>Tests containment with a perpendicular distance tolerance of 0.00001 input units.</summary>
    /// <param name="point">The finite point to test.</param>
    /// <returns>True when the point lies within the default tolerance of the line.</returns>
    public bool ContainsPoint(Vector2 point) => ContainsPoint(point, LinearGeometry2D.DefaultPointTolerance);

    /// <summary>Tests containment with an explicit tolerance; zero requests exact containment.</summary>
    /// <param name="point">The finite point to test.</param>
    /// <param name="tolerance">A nonnegative distance in input units.</param>
    /// <returns>True when the point lies within the tolerance of the line.</returns>
    public bool ContainsPoint(Vector2 point, float tolerance)
    {
        ArgGuard.ThrowIfNegativeOrNotFinite(tolerance);
        return LinearGeometry2D.Distance(point, Origin, Direction, forwardOnly: false) <= tolerance;
    }

    internal void Validate() => UnitDirection.Validate();

    /// <summary>Creates a line from a point and an already-normalized direction.</summary>
    /// <param name="pointOnLine">A finite point on the line.</param>
    /// <param name="direction">A valid unit direction.</param>
    /// <returns>The line through the point along the direction.</returns>
    public static Line2D FromDirection(Vector2 pointOnLine, Direction2D direction) => new(pointOnLine, direction);

    /// <summary>Creates the line through two distinct finite points, directed from the first toward the second.</summary>
    /// <param name="first">The first point; it becomes <see cref="Origin"/>.</param>
    /// <param name="second">A second, distinct point.</param>
    /// <returns>The line through both points.</returns>
    public static Line2D FromPoints(Vector2 first, Vector2 second) => FromDirection(first, Direction2D.FromPoints(first, second));

    /// <summary>The axis-aligned line y = <paramref name="y"/>, directed along +X.</summary>
    /// <param name="y">The finite height of the line.</param>
    /// <returns>A horizontal line.</returns>
    public static Line2D Horizontal(float y) => new(new Vector2(0f, y), Vector2.UnitX);

    /// <summary>The axis-aligned line x = <paramref name="x"/>, directed along +Y.</summary>
    /// <param name="x">The finite horizontal position of the line.</param>
    /// <returns>A vertical line.</returns>
    public static Line2D Vertical(float x) => new(new Vector2(x, 0f), Vector2.UnitY);
}
