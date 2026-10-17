using App2d.Core.Geometry;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct Ellipse2D
{
    public void Validate()
    {
        ArgGuard.Positive(Radii, "radii");
        ArgGuard.Finite(Center, "center");
    }

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static Ellipse2D Create(Vector2 radii, Vector2 center = default) => new Ellipse2D(radii, center).Validated();
    }
}
