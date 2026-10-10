using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>
/// A non-convex shape assembled from convex parts positioned in this shape's local space.
/// Collision, distance and ray casts resolve per part, never against the convex hull, so notches stay empty.
/// </summary>
public sealed class CompositeShape2D : IShape2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.Composite;

    private readonly IConvexShape2D[] _parts;

    /// <summary>Creates a composite and copies its part list.</summary>
    /// <param name="parts">At least one convex part in this shape's local space.</param>
    /// <param name="includeOverlap">True sums part areas; false counts overlapping regions once.</param>
    /// <param name="areaOutlineSegments">Samples for polygonized curves when overlap is counted once. Circles and ellipses stay analytic.</param>
    public CompositeShape2D(IEnumerable<IConvexShape2D> parts, bool includeOverlap = true, int areaOutlineSegments = ShapeArea2D.DefaultOutlineSegments)
    {
        _parts = [.. ArgGuard.RequireNotNull(parts)];
        ArgGuard.ThrowIfTooShort(_parts, 1, nameof(parts));
        ArgGuard.ThrowIfContainsNull(_parts, nameof(parts));
        ArgGuard.ThrowIf(areaOutlineSegments < 3, "Curved outlines need at least three samples.", nameof(areaOutlineSegments));
        IncludeOverlap = includeOverlap;
        AreaOutlineSegments = areaOutlineSegments;

        if (!includeOverlap)
        {
            Area = ShapeArea2D.Union(_parts, areaOutlineSegments);
            return;
        }

        var area = _parts[0].Area;
        foreach (var part in _parts.AsSpan(1)) area += part.Area;
        Area = area;
    }

    /// <summary>The convex parts.</summary>
    public ReadOnlySpan<IConvexShape2D> Parts => _parts;

    /// <summary>Whether <see cref="Area"/> sums part areas, including duplicate coverage.</summary>
    public bool IncludeOverlap { get; }

    /// <summary>The sample count used for polygonized curves when calculating union area.</summary>
    public int AreaOutlineSegments { get; }

    /// <summary>
    /// Cached at construction. With <see cref="IncludeOverlap"/>, this is the sum of part areas and an upper bound.
    /// Otherwise it is union area: analytic for circles, ellipses and polygons, approximate for polygonized curves.
    /// </summary>
    public float Area { get; }

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2 localPoint)
    {
        foreach (var part in _parts)
        {
            if (part.ContainsPoint(localPoint)) return true;
        }
        return false;
    }
}
