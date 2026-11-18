using App2d.Core.Geometry.Operations.ClosestPoint;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Primitives;

/// <summary>
/// An infinite line through <see cref="Origin"/> in both directions along a unit <see cref="Direction"/>.
/// Construct explicitly: the default value has no direction and is rejected by every query.
/// </summary>
public readonly record struct Line2D : IGeometry2D
{
    /// <summary>Creates a line from a point and any finite, nonzero direction; the direction is normalized.</summary>
    /// <param name="origin">A finite point on the line.</param>
    /// <param name="direction">A finite, nonzero direction; length is ignored.</param>
    public Line2D(Vector2f origin, Vector2f direction) : this(origin, new Direction2D(direction)) { }

    private Line2D(Vector2f origin, Direction2D direction)
    {
        Ensure.Finite(origin);
        direction.Validate();
        Origin = origin;
        UnitDirection = direction;
    }

    /// <summary>A point on the line.</summary>
    public Vector2f Origin { get; }

    /// <summary>The unit direction as a vector.</summary>
    public Vector2f Direction => UnitDirection.Vector;

    /// <summary>The validated unit direction.</summary>
    public Direction2D UnitDirection { get; }

    /// <summary>False for the default value, which has no direction.</summary>
    public bool IsValid => UnitDirection.IsValid;

    /// <summary>Returns the point at a signed distance along the line.</summary>
    /// <param name="t">A finite signed distance; negative values run opposite <see cref="Direction"/>.</param>
    /// <returns>Origin + Direction * t.</returns>
    public Vector2f PointAt(float t)
    {
        Validate();
        Ensure.Finite(t);
        return Origin + Direction * t;
    }

    /// <summary>Classifies which side of the line a point lies on.</summary>
    /// <param name="test">The finite point to classify.</param>
    /// <param name="tolerance">A nonnegative distance in input units within which the point counts as on the line.</param>
    /// <returns>+1 on the right (clockwise) side of <see cref="Direction"/>, -1 on the left, or 0 within tolerance of the line.</returns>
    public int WhichSide(Vector2f test, float tolerance = 0f)
    {
        Validate();
        Ensure.Finite(test);
        Ensure.NonNegative(tolerance);
        var delta = test.ToVector2d() - Origin.ToVector2d();
        var determinant = delta.Cross(Direction.ToVector2d());
        return determinant > tolerance ? 1 : determinant < -tolerance ? -1 : 0;
    }

    /// <summary>The closest point on the line to a point.</summary>
    /// <param name="point">The finite query point.</param>
    /// <returns>The perpendicular foot on the line.</returns>
    public Vector2f ClosestPoint(Vector2f point) => ClosestPoint2D.OnLine(point, Origin, Direction);

    /// <summary>The perpendicular distance from a point to the line.</summary>
    /// <param name="point">The finite query point.</param>
    /// <returns>An unsigned distance in input units.</returns>
    public float DistanceTo(Vector2f point) => Distance2D.Distance(point, this);

    /// <summary>Tests containment with a perpendicular distance tolerance of 0.00001 input units.</summary>
    /// <param name="point">The finite point to test.</param>
    /// <returns>True when the point lies within the default tolerance of the line.</returns>
    public bool ContainsPoint(Vector2f point) => ContainsPoint(point, LinearGeometry2D.DefaultPointTolerance);

    /// <summary>Tests containment with an explicit tolerance; zero requests exact containment.</summary>
    /// <param name="point">The finite point to test.</param>
    /// <param name="tolerance">A nonnegative distance in input units.</param>
    /// <returns>True when the point lies within the tolerance of the line.</returns>
    public bool ContainsPoint(Vector2f point, float tolerance)
    {
        Ensure.NonNegative(tolerance);
        return LinearGeometry2D.Distance(point, Origin, Direction, forwardOnly: false) <= tolerance;
    }

    internal void Validate() => UnitDirection.Validate();

    /// <summary>Creates a line from a point and an already-normalized direction.</summary>
    /// <param name="pointOnLine">A finite point on the line.</param>
    /// <param name="direction">A valid unit direction.</param>
    /// <returns>The line through the point along the direction.</returns>
    public static Line2D FromDirection(Vector2f pointOnLine, Direction2D direction) => new(pointOnLine, direction);

    /// <summary>Creates the line through two distinct finite points, directed from the first toward the second.</summary>
    /// <param name="first">The first point; it becomes <see cref="Origin"/>.</param>
    /// <param name="second">A second, distinct point.</param>
    /// <returns>The line through both points.</returns>
    public static Line2D FromPoints(Vector2f first, Vector2f second) => FromDirection(first, Direction2D.FromPoints(first, second));

    /// <summary>The axis-aligned line y = <paramref name="y"/>, directed along +X.</summary>
    /// <param name="y">The finite height of the line.</param>
    /// <returns>A horizontal line.</returns>
    public static Line2D Horizontal(float y) => new(new Vector2f(0f, y), Vector2f.UnitX);

    /// <summary>The axis-aligned line x = <paramref name="x"/>, directed along +Y.</summary>
    /// <param name="x">The finite horizontal position of the line.</param>
    /// <returns>A vertical line.</returns>
    public static Line2D Vertical(float x) => new(new Vector2f(x, 0f), Vector2f.UnitY);
}
