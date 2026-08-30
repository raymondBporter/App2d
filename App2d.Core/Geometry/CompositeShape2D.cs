using System.Numerics;

namespace App2d.Core.Geometry;

/// <summary>
/// A non-convex shape assembled from convex parts positioned in this shape's
/// local space. Collision resolves per part, never against the convex hull.
/// </summary>
public sealed class CompositeShape2D : IShape2D
{
    private readonly IConvexShape2D[] _parts;

    public CompositeShape2D(IEnumerable<IConvexShape2D> parts)
    {
        _parts = [.. ArgGuard.RequireNotNull(parts)];
        ArgGuard.ThrowIfTooShort<IConvexShape2D>(_parts, 1, nameof(parts));

        var bounds = _parts[0].LocalBounds;
        var area = _parts[0].Area;
        foreach (var part in _parts.AsSpan(1))
        {
            bounds = new Bounds2D(Vector2.Min(bounds.Min, part.LocalBounds.Min), Vector2.Max(bounds.Max, part.LocalBounds.Max));
            area += part.Area;
        }

        LocalBounds = bounds;
        Area = area;
    }

    public ReadOnlySpan<IConvexShape2D> Parts => _parts;
    public Bounds2D LocalBounds { get; }

    /// <summary>Overlapping parts double-count; treat as an upper bound.</summary>
    public float Area { get; }

    public bool ContainsPoint(Vector2 localPoint)
    {
        foreach (var part in _parts)
        {
            if (part.ContainsPoint(localPoint))
                return true;
        }

        return false;
    }
}
