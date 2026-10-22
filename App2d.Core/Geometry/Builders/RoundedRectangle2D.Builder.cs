using App2d.Core.Geometry;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct RoundedRectangle2D
{
    public void Validate()
    {
        ArgGuard.Finite(Center);
        ArgGuard.Finite(HalfSize);
        ArgGuard.LessThan(-HalfSize, HalfSize);
        ArgGuard.InRange((double)Radius, 0, Math.Min((double)HalfSize.X, (double)HalfSize.Y), "radius");
        OrientedFrame2D.ValidateAxis(Axis, "axis");
    }

    /// <summary>The same center, axis and corner radius at a new outer size, with the radius clamped to fit.</summary>
    public RoundedRectangle2D WithSize(Vector2 size) => Builder.Create(Center, size / 2f, MathF.Min(Radius, MathF.Min(size.X, size.Y) / 2), Axis);

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        /// <summary>An axis-aligned rounded rectangle from ordered outer corners.</summary>
        public static RoundedRectangle2D Create(Vector2 min, Vector2 max, float radius) => new RoundedRectangle2D((min + max) / 2f, (max - min) / 2f, radius, Vector2.UnitX).Validated();

        /// <summary>A rounded rectangle whose local +X follows any finite nonzero direction; its length is ignored.</summary>
        public static RoundedRectangle2D Create(Vector2 center, Vector2 halfSize, float radius, Vector2 axis) => new RoundedRectangle2D(center, halfSize, radius, new Direction2D(axis).Vector).Validated();
        public static RoundedRectangle2D FromSize(float width, float height, float radius, Vector2 center = default, float angleRadians = 0f) => FromSize(new Vector2(width, height), radius, center, angleRadians);
        public static RoundedRectangle2D Create(float width, float height, float radius, Vector2 center = default, float angleRadians = 0f) => FromSize(width, height, radius, center, angleRadians);
        public static RoundedRectangle2D CreateFromSize(float width, float height, float radius, Vector2 center = default) => Create(width, height, radius, center);
        public static RoundedRectangle2D FromSize(Vector2 size, float radius, Vector2 center = default, float angleRadians = 0f) => new RoundedRectangle2D(center, size / 2f, radius, OrientedFrame2D.AxisFromAngle(angleRadians)).Validated();
        public static RoundedRectangle2D CreateFromSize(Vector2 size, float radius, Vector2 center = default) => FromSize(size, radius, center);
    }
}
