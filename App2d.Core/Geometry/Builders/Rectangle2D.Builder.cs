using App2d.Core.Geometry.Builders;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Shapes;

public readonly partial record struct Rectangle2D
{
    /// <summary>Editable construction data; Build validates before creating an immutable shape.</summary>
    public sealed record Builder : ConvexShapeBuilder2D
    {
        public required Vector2 Min { get; init; }
        public required Vector2 Max { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool AxisAligned { get; init; }

        [JsonIgnore] public override string Kind => ShapeKinds2D.Rectangle;
        public override void Validate()
        {
            ValidateBounds(Min, Max);
        }

        protected override IConvexShape2D BuildShape() => Build();

        // This builder can produce either of two independent value types.
        public new IConvexShape2D Build()
        {
            Validate();
            return AxisAligned ? AxisAlignedRectangle2D.Builder.CreateValidated(Min, Max) : new Rectangle2D(Min, Max);
        }

        private Rectangle2D BuildRectangle()
        {
            Validate();
            return new Rectangle2D(Min, Max);
        }

        public static Rectangle2D Create(Vector2 min, Vector2 max) => new Builder { Min = min, Max = max }.BuildRectangle();
        internal static void ValidateBounds(Vector2 min, Vector2 max) => ArgGuard.LessThan(min, max);
        public static Builder FromSize(float width, float height, Vector2 center = default) => FromSize(new Vector2(width, height), center);
        public static Rectangle2D Create(float width, float height, Vector2 center = default) => FromSize(width, height, center).BuildRectangle();
        public static Rectangle2D CreateFromSize(float width, float height, Vector2 center = default) => Create(width, height, center);
        public static Builder FromSize(Vector2 size, Vector2 center = default)
        {
            ArgGuard.Positive(size);
            ArgGuard.Finite(center);
            return new() { Min = center - size / 2f, Max = center + size / 2f };
        }

        public static Builder FromDimensions(Size2D size, Vector2 center = default)
        {
            ArgGuard.ThrowIf(!size.IsValid, "Size must have positive finite dimensions.", nameof(size));
            return FromSize(size.ToVector2(), center);
        }

        public static Builder FromMinAndSize(Vector2 min, Size2D size)
        {
            ArgGuard.Finite(min);
            ArgGuard.ThrowIf(!size.IsValid, "Size must have positive finite dimensions.", nameof(size));
            return new()
            {
                Min = min,
                Max = min + size.ToVector2()
            };
        }
        public static Rectangle2D CreateFromSize(Vector2 size, Vector2 center = default) => FromSize(size, center).BuildRectangle();
        public static Rectangle2D CreateFromSize(Size2D size, Vector2 center = default) => FromDimensions(size, center).BuildRectangle();
        public static Rectangle2D CreateFromMinAndSize(Vector2 min, Size2D size) => FromMinAndSize(min, size).BuildRectangle();
    }
}
