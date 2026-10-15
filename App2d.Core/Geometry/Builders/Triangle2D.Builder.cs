using App2d.Core.Geometry.Builders;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Shapes;

public sealed partial class Triangle2D
{
    /// <summary>Editable construction data; Build validates before creating an immutable shape.</summary>
    public sealed record Builder : ConvexShapeBuilder2D
    {
        public required Vector2 A { get; init; }
        public required Vector2 B { get; init; }
        public required Vector2 C { get; init; }
        [JsonIgnore] public override string Kind => ShapeKinds2D.Triangle;
        private double SignedAreaTwice()
        {
            ArgGuard.ThrowIfNotFinite(A, "a"); ArgGuard.ThrowIfNotFinite(B, "b"); ArgGuard.ThrowIfNotFinite(C, "c");
            var twice = A.Orientation(B, C);
            if (twice == 0d) throw new ArgumentException("Triangle vertices must not be collinear.", "c");
            ArgGuard.ThrowIfNotFiniteOrNotPositive((float)(Math.Abs(twice) * .5), "area");
            return twice;
        }
        public override void Validate() => SignedAreaTwice();
        public override Triangle2D Build() => new(A, B, C, SignedAreaTwice());
        public static Triangle2D Create(Vector2 a, Vector2 b, Vector2 c) => new Builder { A = a, B = b, C = c }.Build();
    }
}
