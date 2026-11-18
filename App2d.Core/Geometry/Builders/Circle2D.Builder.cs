using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Construction;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;

namespace App2d.Core.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct Circle2D
{
    public void Validate()
    {
        Ensure.Positive(Radius, "radius");
        Ensure.Finite(Center, "center");
    }

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static Circle2D Create(float radius, Vector2f center = default) => new Circle2D(radius, center).Validated();

        public static Circle2D FromPoints(Vector2f a, Vector2f b, Vector2f c)
        {
            var (center, radius) = CircleConstruction2D.ThroughPoints(a, b, c);
            return Create(radius, center);
        }
    }
}
