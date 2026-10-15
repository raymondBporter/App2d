using App2d.Core.Geometry.Builders;
using App2d.Core.Geometry.Operations.Area;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Validation;
using System.Text.Json.Serialization;

namespace App2d.Core.Shapes;

public sealed partial class CompositeShape2D
{
    /// <summary>Editable construction data; Build validates before creating an immutable shape.</summary>
    public sealed record Builder : ShapeBuilder2D
    {
        public required List<ShapeBuilder2D> Parts
        {
            get; init;
        }
        public bool IncludeOverlap { get; init; } = true;
        public int AreaOutlineSegments { get; init; } = ShapeArea2D.DefaultOutlineSegments;
        [JsonIgnore] public override string Kind => ShapeKinds2D.Composite;
        private static ConvexShapeBuilder2D ConvexPart(ShapeBuilder2D part) =>
            ArgGuard.RequireNotNull(part, "parts") as ConvexShapeBuilder2D
            ?? throw new InvalidOperationException("Composite parts must be convex.");
        private IConvexShape2D[] BuildParts() => [.. ArgGuard.RequireNotNull(Parts, "parts").Select(part => ConvexPart(part).Build())];
        private static void ValidateParts(IConvexShape2D[] parts, int areaOutlineSegments)
        {
            ArgGuard.ThrowIfTooShort(parts, 1, nameof(parts));
            ArgGuard.ThrowIfContainsNull(parts, nameof(parts));
            ArgGuard.ThrowIf(areaOutlineSegments < 3, "Curved outlines need at least three samples.", nameof(areaOutlineSegments));
        }
        public override void Validate()
        {
            ArgGuard.ThrowIfTooShort<ShapeBuilder2D>(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(ArgGuard.RequireNotNull(Parts, "parts")), 1, "parts");
            ArgGuard.ThrowIf(AreaOutlineSegments < 3, "Curved outlines need at least three samples.", "areaOutlineSegments");
            foreach (var part in Parts)
            {
                ConvexPart(part).Validate();
            }
        }
        public override CompositeShape2D Build()
        {
            var parts = BuildParts();
            ValidateParts(parts, AreaOutlineSegments);
            return new(parts, IncludeOverlap, AreaOutlineSegments);
        }
        public static CompositeShape2D Create(IEnumerable<IConvexShape2D> parts, bool includeOverlap = true, int areaOutlineSegments = ShapeArea2D.DefaultOutlineSegments)
        {
            IConvexShape2D[] snapshot = [.. ArgGuard.RequireNotNull(parts)];
            ValidateParts(snapshot, areaOutlineSegments);
            return new(snapshot, includeOverlap, areaOutlineSegments);
        }
    }
}
