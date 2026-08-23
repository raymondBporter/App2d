using System.Numerics;

namespace App2d.Engine.Geometry;

// Intent marker for rectangles kept axis-aligned in world space by their owner.
// This enables future slab/broad-phase optimizations while sharing Rectangle2D geometry.
public sealed class AxisAlignedRectangle2D(Vector2 min, Vector2 max) : Rectangle2D(min, max)
{
    public static new AxisAlignedRectangle2D FromSize(Vector2 size, Vector2 center = default)
    {
        if (!float.IsFinite(size.X) || !float.IsFinite(size.Y) || size.X <= 0f || size.Y <= 0f)
            throw new ArgumentOutOfRangeException(nameof(size), "Size must be positive and finite.");

        var halfSize = size / 2f;
        return new AxisAlignedRectangle2D(center - halfSize, center + halfSize);
    }
}
