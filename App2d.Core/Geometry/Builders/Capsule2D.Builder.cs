using App2d.Core.Geometry;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct Capsule2D
{
    public void Validate()
    {
        ArgGuard.Finite(Start, "start");
        ArgGuard.Finite(End, "end");
        ArgGuard.Positive(Radius, "radius");
    }

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static Capsule2D Create(Vector2 start, Vector2 end, float radius) => new Capsule2D(start, end, radius).Validated();
    }
}
