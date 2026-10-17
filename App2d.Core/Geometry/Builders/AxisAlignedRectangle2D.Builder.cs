using App2d.Core.Geometry.Primitives;
using System.Numerics;

namespace App2d.Core.Shapes;

public readonly partial record struct AxisAlignedRectangle2D
{
    /// <summary>Rectangle factories retaining axis-aligned authoring intent.</summary>
    public static class Builder
    {
        internal static AxisAlignedRectangle2D CreateValidated(Vector2 min, Vector2 max) => new(min, max);
        public static AxisAlignedRectangle2D Create(Vector2 min, Vector2 max) => new(min, max);
        private static AxisAlignedRectangle2D CreateFromDefinition(Rectangle2D.Builder definition) => new(definition.Min, definition.Max);
        public static Rectangle2D.Builder FromSize(float width, float height, Vector2 center = default) => FromSize(new Vector2(width, height), center);
        public static AxisAlignedRectangle2D Create(float width, float height, Vector2 center = default) => CreateFromDefinition(FromSize(width, height, center));
        public static AxisAlignedRectangle2D CreateFromSize(float width, float height, Vector2 center = default) => Create(width, height, center);

        public static Rectangle2D.Builder FromSize(Vector2 size, Vector2 center = default) => Rectangle2D.Builder.FromSize(size, center) with { AxisAligned = true };
        public static Rectangle2D.Builder FromDimensions(Size2D size, Vector2 center = default) => Rectangle2D.Builder.FromDimensions(size, center) with { AxisAligned = true };
        public static Rectangle2D.Builder FromMinAndSize(Vector2 min, Size2D size) => Rectangle2D.Builder.FromMinAndSize(min, size) with { AxisAligned = true };
        public static AxisAlignedRectangle2D CreateFromSize(Vector2 size, Vector2 center = default) => CreateFromDefinition(FromSize(size, center));
        public static AxisAlignedRectangle2D CreateFromSize(Size2D size, Vector2 center = default) => CreateFromDefinition(FromDimensions(size, center));
        public static AxisAlignedRectangle2D CreateFromMinAndSize(Vector2 min, Size2D size) => CreateFromDefinition(FromMinAndSize(min, size));
    }
}
