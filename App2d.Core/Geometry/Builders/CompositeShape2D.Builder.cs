using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Area;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Validation;

namespace App2d.Core.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct CompositeShape2D
{
    public void Validate()
    {
        Ensure.ThrowIfTooShort(Parts, 1, "parts");
        Ensure.ThrowIfContainsNull(Parts, "parts");
        Ensure.GreaterThanOrEqual(AreaOutlineSegments, 3);
        foreach (var part in Parts)
        {
            if (part is not IConvexShape2D) throw new InvalidOperationException("Composite parts must be convex.");
            part.Validate();
        }
    }
    public static class Builder
    {
        public static CompositeShape2D Create(IEnumerable<IConvexShape2D> parts, bool includeOverlap = true, int areaOutlineSegments = ShapeArea2D.DefaultOutlineSegments) =>
            new CompositeShape2D([.. Ensure.RequireNotNull(parts)], includeOverlap, areaOutlineSegments).Validated();
    }
}
