using App2d.Core.Geometry.Builders;
using App2d.Core.Geometry.Operations.Construction;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Shapes;

public sealed partial class Circle2D
{
    /// <summary>Editable construction data; Build validates before creating an immutable shape.</summary>
    public sealed record Builder : ConvexShapeBuilder2D
    {
        public required Vector2 Center { get; init; }
        public required float Radius { get; init; }
        [JsonIgnore] public override string Kind => ShapeKinds2D.Circle;
        public override void Validate()
        {
            ArgGuard.ThrowIfNotFiniteOrNotPositive(Radius, "radius");
            ArgGuard.ThrowIfNotFinite(Center, "center");
        }
        public override Circle2D Build()
        {
            Validate();
            return new Circle2D(Radius, Center);
        }
        public static Circle2D Create(float radius, Vector2 center = default) => new Builder { Center = center, Radius = radius }.Build();

        public static Builder FromPoints(Vector2 a, Vector2 b, Vector2 c)
        {
            var (center, radius) = CircleConstruction2D.ThroughPoints(a, b, c);
            return new() { Center = center, Radius = radius };
        }
        public static Circle2D CreateFromPoints(Vector2 a, Vector2 b, Vector2 c) => FromPoints(a, b, c).Build();
    }
}
