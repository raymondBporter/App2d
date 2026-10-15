using App2d.Core.Geometry;
using App2d.Core.Geometry.Builders;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Shapes;

public sealed partial class RoundedRectangle2D
{
    /// <summary>Editable construction data; Build validates before creating an immutable shape.</summary>
    public sealed record Builder : ConvexShapeBuilder2D
    {
        public required Vector2 Min { get; init; }
        public required Vector2 Max { get; init; }
        public required float Radius { get; init; }
        [JsonIgnore] public override string Kind => ShapeKinds2D.RoundedRectangle;
        public override void Validate()
        {
            Rectangle2D.Builder.ValidateBounds(Min, Max);
            ArgGuard.ThrowIfNotFiniteOrNegative(Radius, "radius");
            if (Radius > MathF.Min(Max.X - Min.X, Max.Y - Min.Y) / 2f)
                throw new ArgumentOutOfRangeException("radius", "Corner radius cannot exceed half the shorter side.");
        }
        public override RoundedRectangle2D Build()
        {
            Validate();
            return new RoundedRectangle2D(Min, Max, Radius);
        }
        public static RoundedRectangle2D Create(Vector2 min, Vector2 max, float radius) => new Builder { Min = min, Max = max, Radius = radius }.Build();

        public static Builder FromSize(Vector2 size, float radius, Vector2 center = default)
        {
            var rectangle = Rectangle2D.Builder.FromSize(size, center);
            return new() { Min = rectangle.Min, Max = rectangle.Max, Radius = radius };
        }
        public static RoundedRectangle2D CreateFromSize(Vector2 size, float radius, Vector2 center = default) => FromSize(size, radius, center).Build();
    }
}
