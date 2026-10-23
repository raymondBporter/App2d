using App2d.Core.Geometry;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct Rectangle2D
{
    public void Validate()
    {
        ArgGuard.Finite(Center);
        ArgGuard.Finite(HalfSize);
        // Positive half extents are the ordered-corner rule of every rectangle, so the same guard and exception apply.
        ArgGuard.LessThan(-HalfSize, HalfSize);
        OrientedFrame2D.ValidateAxis(Axis, "axis");
    }

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        /// <summary>An axis-aligned rectangle from ordered corners.</summary>
        public static Rectangle2D Create(Vector2 min, Vector2 max) => new Rectangle2D((min + max) / 2f, (max - min) / 2f, Vector2.UnitX).Validated();

        /// <summary>A rectangle whose local +X follows any finite nonzero direction; its length is ignored.</summary>
        public static Rectangle2D Create(Vector2 center, Vector2 halfSize, Vector2 axis) => new Rectangle2D(center, halfSize, new Direction2D(axis).Vector).Validated();
        public static Rectangle2D FromSize(float width, float height, Vector2 center = default, float angleRadians = 0f) => FromSize(new Vector2(width, height), center, angleRadians);
        public static Rectangle2D Create(float width, float height, Vector2 center = default, float angleRadians = 0f) => FromSize(width, height, center, angleRadians);
        public static Rectangle2D CreateFromSize(float width, float height, Vector2 center = default) => Create(width, height, center);
        public static Rectangle2D FromSize(Vector2 size, Vector2 center = default, float angleRadians = 0f) => new Rectangle2D(center, size / 2f, OrientedFrame2D.AxisFromAngle(angleRadians)).Validated();
        public static Rectangle2D FromDimensions(Size2D size, Vector2 center = default) => FromSize(size.ToVector2(), center);
        public static Rectangle2D FromMinAndSize(Vector2 min, Size2D size) => Create(min, min + size.ToVector2());
        public static Rectangle2D CreateFromSize(Vector2 size, Vector2 center = default) => FromSize(size, center);
        public static Rectangle2D CreateFromSize(Size2D size, Vector2 center = default) => FromDimensions(size, center);
        public static Rectangle2D CreateFromMinAndSize(Vector2 min, Size2D size) => FromMinAndSize(min, size);
    }
}
