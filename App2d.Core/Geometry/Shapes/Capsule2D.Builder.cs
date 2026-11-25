using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct Capsule2
{
    public void Validate()
    {
        Ensure.Finite(Start, "start");
        Ensure.Finite(End, "end");
        Ensure.Positive(Radius, "radius");
    }

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static Capsule2 Create(Vector2f start, Vector2f end, float radius) => new Capsule2(start, end, radius).Validated();
    }
}
