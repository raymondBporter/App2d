using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Construction;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct Circle2D
{
    public void Validate()
    {
        ArgGuard.Positive(Radius, "radius");
        ArgGuard.Finite(Center, "center");
    }

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static Circle2D Create(float radius, Vector2 center = default) => new Circle2D(radius, center).Validated();
        
        public static Circle2D FromPoints(Vector2 a, Vector2 b, Vector2 c)
        {
            var (center, radius) = CircleConstruction2D.ThroughPoints(a, b, c);
            return Create(radius, center);
        }
    }
}
