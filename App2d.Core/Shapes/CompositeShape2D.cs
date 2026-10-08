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
    public CompositeShape2D(IEnumerable<IConvexShape2D> parts)
    {
        _parts = [.. ArgGuard.RequireNotNull(parts)];
        ArgGuard.ThrowIfTooShort(_parts, 1, nameof(parts));

        var area = _parts[0].Area;
        foreach (var part in _parts.AsSpan(1)) area += part.Area;
        Area = area;
    }

    /// <summary>The convex parts.</summary>
    public ReadOnlySpan<IConvexShape2D> Parts => _parts;

    /// <summary>The sum of part areas; overlapping parts double-count, so treat this as an upper bound.</summary>
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
