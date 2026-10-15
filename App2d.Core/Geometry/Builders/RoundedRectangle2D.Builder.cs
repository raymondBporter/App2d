using App2d.Core.Geometry.Builders;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Shapes;

public sealed partial class RoundedRectangle2D
{
    /// <summary>Editable construction data; Build validates before creating an immutable shape.</summary>
    public sealed record Builder : ConvexShapeBuilder2D
    {
        /// <summary>The outer rectangle's minimum corner.</summary>
        public required Vector2 Min { get; init; }
        /// <summary>The outer rectangle's maximum corner.</summary>
        public required Vector2 Max { get; init; }
        public required float Radius { get; init; }
        [JsonIgnore] public override string Kind => ShapeKinds2D.RoundedRectangle;
        public override void Validate()
        {
            Rectangle2D.Builder.ValidateBounds(Min, Max);
            ArgGuard.ThrowIfNotFiniteOrNegative(Radius, "radius");
            if (Radius > Math.Min((double)Max.X - Min.X, (double)Max.Y - Min.Y) / 2)
                throw new ArgumentOutOfRangeException("radius", "Corner radius cannot exceed half the shorter side.");
        }
        public override RoundedRectangle2D Build()
        {
            Validate();
            return new RoundedRectangle2D(Min, Max, Radius);
        }
        public static RoundedRectangle2D Create(Vector2 min, Vector2 max, float radius) => new Builder { Min = min, Max = max, Radius = radius }.Build();

        public static Builder FromSize(float width, float height, float radius, Vector2 center = default) => FromSize(new Vector2(width, height), radius, center);
        public static RoundedRectangle2D Create(float width, float height, float radius, Vector2 center = default) => FromSize(width, height, radius, center).Build();
        public static RoundedRectangle2D CreateFromSize(float width, float height, float radius, Vector2 center = default) => Create(width, height, radius, center);

        public static Builder FromSize(Vector2 size, float radius, Vector2 center = default)
        {
            var rectangle = Rectangle2D.Builder.FromSize(size, center);
            return new() { Min = rectangle.Min, Max = rectangle.Max, Radius = radius };
        }
        /// <summary>Resizes around the existing center, keeping the corner radius within the new sides.</summary>
        public Builder WithSize(Vector2 size) => FromSize(size, MathF.Min(Radius, MathF.Min(size.X, size.Y) / 2), new Rect2D(Min, Max).Center);
        public static RoundedRectangle2D CreateFromSize(Vector2 size, float radius, Vector2 center = default) => FromSize(size, radius, center).Build();
    }
}
