using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>
/// An intent marker for rectangles that their owner keeps axis-aligned in world space. It shares all
/// <see cref="Rectangle2D"/> geometry and enables future slab and broad-phase shortcuts.
/// </summary>
/// <param name="min">The finite lower-left corner.</param>
/// <param name="max">The finite upper-right corner, component-wise greater than <paramref name="min"/>.</param>
public sealed class AxisAlignedRectangle2D(Vector2 min, Vector2 max) : Rectangle2D(min, max)
{
    /// <summary>Creates an axis-aligned rectangle of a given size around a center.</summary>
    /// <param name="size">The finite, positive width and height.</param>
    /// <param name="center">The finite local center.</param>
    /// <returns>The rectangle spanning half the size on each side of the center.</returns>
    public static new AxisAlignedRectangle2D FromSize(Vector2 size, Vector2 center = default)
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(size);
        ArgGuard.ThrowIfNotFinite(center);
        var halfSize = size / 2f;
        return new AxisAlignedRectangle2D(center - halfSize, center + halfSize);
    }
}
