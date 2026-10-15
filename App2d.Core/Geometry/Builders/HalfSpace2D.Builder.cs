using App2d.Core.Geometry;
using App2d.Core.Geometry.Builders;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Shapes;

public sealed partial class HalfSpace2D
{
    /// <summary>Editable construction data; Build validates before creating an immutable shape.</summary>
    public sealed record Builder : ShapeBuilder2D
    {
        public required Vector2 Normal { get; init; }
        public required float Offset { get; init; }
        [JsonIgnore] public override string Kind => ShapeKinds2D.HalfSpace;
        public override void Validate()
        {
            ArgGuard.ThrowIfNotFiniteOrZero(Normal, "outwardNormal");
            ArgGuard.ThrowIfNotFinite(Offset, "offset");
        }
        public override HalfSpace2D Build()
        {
            Validate();
            var wideNormal = Normal.ToVector2d();
            var normalLength = wideNormal.Length();
            var normal = (wideNormal / normalLength).ToVector2();
            var offset = (float)(Offset / normalLength);
            ArgGuard.ThrowIfNotFinite(offset, "offset");
            return new HalfSpace2D(normal, offset);
        }
        public static HalfSpace2D Create(Vector2 outwardNormal, float offset) => new Builder { Normal = outwardNormal, Offset = offset }.Build();

        public static Builder FromPoint(Vector2 pointOnBoundary, Vector2 outwardNormal)
        {
            ArgGuard.ThrowIfNotFinite(pointOnBoundary);
            ArgGuard.ThrowIfNotFiniteOrZero(outwardNormal);
            var wideNormal = outwardNormal.ToVector2d();
            var normal = (wideNormal / wideNormal.Length()).ToVector2();
            return new() { Normal = normal, Offset = (float)pointOnBoundary.ToVector2d().Dot(normal.ToVector2d()) };
        }
        public static HalfSpace2D CreateFromPoint(Vector2 pointOnBoundary, Vector2 outwardNormal) => FromPoint(pointOnBoundary, outwardNormal).Build();
    }
}
