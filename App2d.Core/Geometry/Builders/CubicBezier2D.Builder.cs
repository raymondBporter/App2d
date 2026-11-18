using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Curves;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct CubicBezier2D
{
    public void Validate()
    {
        Ensure.Finite(Start, "start");
        Ensure.Finite(Control1, "control1");
        Ensure.Finite(Control2, "control2");
        Ensure.Finite(End, "end");
    }

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static CubicBezier2D Create(Vector2f start, Vector2f control1, Vector2f control2, Vector2f end) => new CubicBezier2D(start, control1, control2, end).Validated();
    }
}
