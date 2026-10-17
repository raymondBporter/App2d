using App2d.Core.Geometry;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using System.Numerics;

namespace App2d.Core.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct AxisAlignedRectangle2D
{
    public void Validate() => Rectangle2D.ValidateBounds(Min, Max);

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static AxisAlignedRectangle2D Create(Vector2 min, Vector2 max) => new AxisAlignedRectangle2D(min, max).Validated();
        public static AxisAlignedRectangle2D FromSize(float width, float height, Vector2 center = default) => FromSize(new Vector2(width, height), center);
        public static AxisAlignedRectangle2D Create(float width, float height, Vector2 center = default) => FromSize(width, height, center);
        public static AxisAlignedRectangle2D CreateFromSize(float width, float height, Vector2 center = default) => Create(width, height, center);
        public static AxisAlignedRectangle2D FromSize(Vector2 size, Vector2 center = default) => Create(center - size / 2, center + size / 2);
        public static AxisAlignedRectangle2D FromDimensions(Size2D size, Vector2 center = default) => FromSize(size.ToVector2(), center);
        public static AxisAlignedRectangle2D FromMinAndSize(Vector2 min, Size2D size) => Create(min, min + size.ToVector2());
        public static AxisAlignedRectangle2D CreateFromSize(Vector2 size, Vector2 center = default) => FromSize(size, center);
        public static AxisAlignedRectangle2D CreateFromSize(Size2D size, Vector2 center = default) => FromDimensions(size, center);
        public static AxisAlignedRectangle2D CreateFromMinAndSize(Vector2 min, Size2D size) => FromMinAndSize(min, size);
    }
}
