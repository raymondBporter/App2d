using App2d.Core.Geometry;
using System.Numerics;

namespace App2d.Core.Shapes;

public sealed partial class AxisAlignedRectangle2D
{
    /// <summary>Rectangle factories retaining axis-aligned authoring intent.</summary>
    public new static class Builder
    {
        internal static AxisAlignedRectangle2D CreateValidated(Vector2 min, Vector2 max) => new(min, max);
        public static AxisAlignedRectangle2D Create(Vector2 min, Vector2 max) => (AxisAlignedRectangle2D)new Rectangle2D.Builder { Min = min, Max = max, AxisAligned = true }.Build();
        public static Rectangle2D.Builder FromSize(Vector2 size, Vector2 center = default) => Rectangle2D.Builder.FromSize(size, center) with { AxisAligned = true };
        public static Rectangle2D.Builder FromDimensions(Size2D size, Vector2 center = default) => Rectangle2D.Builder.FromDimensions(size, center) with { AxisAligned = true };
        public static Rectangle2D.Builder FromMinAndSize(Vector2 min, Size2D size) => Rectangle2D.Builder.FromMinAndSize(min, size) with { AxisAligned = true };
        public static AxisAlignedRectangle2D CreateFromSize(Vector2 size, Vector2 center = default) => (AxisAlignedRectangle2D)FromSize(size, center).Build();
        public static AxisAlignedRectangle2D CreateFromSize(Size2D size, Vector2 center = default) => (AxisAlignedRectangle2D)FromDimensions(size, center).Build();
        public static AxisAlignedRectangle2D CreateFromMinAndSize(Vector2 min, Size2D size) => (AxisAlignedRectangle2D)FromMinAndSize(min, size).Build();
    }
}
