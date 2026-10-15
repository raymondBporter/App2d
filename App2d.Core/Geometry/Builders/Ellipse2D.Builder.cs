using App2d.Core.Geometry.Builders;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Shapes;

public sealed partial class Ellipse2D
{
    /// <summary>Editable construction data; Build validates before creating an immutable shape.</summary>
    public sealed record Builder : ConvexShapeBuilder2D
    {
        public required Vector2 Center { get; init; }
        public required Vector2 Radii { get; init; }
        [JsonIgnore] public override string Kind => ShapeKinds2D.Ellipse;
        public override void Validate()
        {
            ArgGuard.ThrowIfNotFiniteOrNotPositive(Radii, "radii");
            ArgGuard.ThrowIfNotFinite(Center, "center");
        }
        public override Ellipse2D Build()
        {
            Validate();
            return new Ellipse2D(Radii, Center);
        }
        public static Ellipse2D Create(Vector2 radii, Vector2 center = default) => new Builder { Center = center, Radii = radii }.Build();
    }
}
