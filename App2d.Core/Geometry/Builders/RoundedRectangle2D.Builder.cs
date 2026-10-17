using App2d.Core.Geometry;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct RoundedRectangle2D
{
    public void Validate()
    {
        Rectangle2D.ValidateBounds(Min, Max);
        ArgGuard.InRange((double)Radius, 0, Math.Min((double)Max.X - Min.X, (double)Max.Y - Min.Y) / 2, "radius");
    }

    public RoundedRectangle2D WithSize(Vector2 size) => Builder.FromSize(size, MathF.Min(Radius, MathF.Min(size.X, size.Y) / 2), (Min + Max) / 2);

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static RoundedRectangle2D Create(Vector2 min, Vector2 max, float radius) => new RoundedRectangle2D(min, max, radius).Validated();
        public static RoundedRectangle2D FromSize(float width, float height, float radius, Vector2 center = default) => FromSize(new Vector2(width, height), radius, center);
        public static RoundedRectangle2D Create(float width, float height, float radius, Vector2 center = default) => FromSize(width, height, radius, center);
        public static RoundedRectangle2D CreateFromSize(float width, float height, float radius, Vector2 center = default) => Create(width, height, radius, center);
        public static RoundedRectangle2D FromSize(Vector2 size, float radius, Vector2 center = default) => Create(center - size / 2, center + size / 2, radius);
        public static RoundedRectangle2D CreateFromSize(Vector2 size, float radius, Vector2 center = default) => FromSize(size, radius, center);
    }
}
