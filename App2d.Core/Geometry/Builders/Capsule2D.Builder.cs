using App2d.Core.Geometry.Builders;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Shapes;

public sealed partial class Capsule2D
{
    /// <summary>Editable construction data; Build validates before creating an immutable shape.</summary>
    public sealed record Builder : ConvexShapeBuilder2D
    {
        public required Vector2 Start
        {
            get; init;
        }
        public required Vector2 End
        {
            get; init;
        }
        public required float Radius
        {
            get; init;
        }
        [JsonIgnore] public override string Kind => ShapeKinds2D.Capsule;
        public override void Validate()
        {
            ArgGuard.Finite(Start, "start");
            ArgGuard.Finite(End, "end");
            ArgGuard.Positive(Radius, "radius");
        }
        public override Capsule2D Build()
        {
            Validate();
            return new Capsule2D(Start, End, Radius);
        }
        public static Capsule2D Create(Vector2 start, Vector2 end, float radius) => new Builder { Start = start, End = end, Radius = radius }.Build();
    }
}
