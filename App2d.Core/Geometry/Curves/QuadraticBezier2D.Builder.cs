using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Curves;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct QuadraticBezier2D
{
    public void Validate()
    {
        Ensure.Finite(Start, "start");
        Ensure.Finite(Control, "control");
        Ensure.Finite(End, "end");
    }

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static QuadraticBezier2D Create(Vector2f start, Vector2f control, Vector2f end) => new QuadraticBezier2D(start, control, end).Validated();
    }
}
