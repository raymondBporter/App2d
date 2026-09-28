using App2d.Core.Geometry.Functions;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Geometry.Shapes;

/// <summary>
/// An infinite line through Origin in both directions along a unit Direction.
/// Construct explicitly: default has no direction and is rejected by queries.
/// </summary>
public readonly record struct Line2D : IGeometry2D
{
    private readonly Direction2D _unitDirection;

    public Line2D(Vector2 origin, Vector2 direction) : this(origin, new Direction2D(direction)) { }

    private Line2D(Vector2 origin, Direction2D direction)
    {
        ArgGuard.ThrowIfNotFinite(origin);
        direction.Validate();
        Origin = origin;
        _unitDirection = direction;
    }

    public Vector2 Origin { get; }
    public Vector2 Direction => _unitDirection.Vector;
    public Direction2D UnitDirection => _unitDirection;
    public bool IsValid => _unitDirection.IsValid;

    /// <summary>Point at signed distance along the line; negative values run opposite Direction.</summary>
    public Vector2 PointAt(float t)
    {
        Validate();
        ArgGuard.ThrowIfNotFinite(t);
        return Origin + Direction * t;
    }

    public Vector2 GetPoint(float distance) => PointAt(distance);

    /// <summary>
    /// Returns +1 on the right (clockwise) side of Direction, -1 on the left,
    /// or 0 within tolerance of the line. Tolerance is a nonnegative distance in input units.
    /// </summary>
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

    internal void Validate() => _unitDirection.Validate();

    public static Line2D FromDirection(Vector2 pointOnLine, Direction2D direction) => new(pointOnLine, direction);

    public static Line2D FromPoints(Vector2 first, Vector2 second) =>
        FromDirection(first, Direction2D.FromPoints(first, second));
}
