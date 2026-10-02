using App2d.Core.Validation;
using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Contracts.World;

/// <summary>A named rectangular region in world units, independent of any zone consumer.</summary>
public sealed record WorldZone2D
{
    public WorldZone2D(string id, string name, Rect2D bounds, int priority = 0)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(id);
        ArgGuard.ThrowIfNullOrWhiteSpace(name);
        if (!bounds.IsFinite || !float.IsFinite(bounds.Size.X) || !float.IsFinite(bounds.Size.Y) ||
            bounds.Size.X <= 0 || bounds.Size.Y <= 0)
            throw new ArgumentOutOfRangeException(nameof(bounds), "Zones need finite, positive rectangles.");
        Id = id; Name = name; Bounds = bounds; Priority = priority;
    }

    public string Id { get; }
    public string Name { get; }
    public Rect2D Bounds { get; }
    public int Priority { get; }

    // Half-open bounds assign a shared edge to exactly one adjacent rectangle.
    public bool Contains(Vector2 point) => point.X >= Bounds.Min.X && point.X < Bounds.Max.X &&
        point.Y >= Bounds.Min.Y && point.Y < Bounds.Max.Y;

    /// <summary>Highest priority wins; equal priorities use ordinal ID, independent of file order.</summary>
    public static WorldZone2D? FindAt(ReadOnlySpan<WorldZone2D> zones, Vector2 point)
    {
        ArgGuard.ThrowIfNotFinite(point);
        WorldZone2D? result = null;
        foreach (var zone in zones)
        {
            if (zone.Contains(point) && (result is null || zone.Priority > result.Priority || (zone.Priority == result.Priority && string.CompareOrdinal(zone.Id, result.Id) < 0)))
            {
                result = zone;
            }
        }

        return result;
    }
}
