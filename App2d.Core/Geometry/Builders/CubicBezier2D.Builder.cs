using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Curves;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct CubicBezier2D
{
    public void Validate()
    {
        ArgGuard.Finite(Start, "start");
        ArgGuard.Finite(Control1, "control1");
        ArgGuard.Finite(Control2, "control2");
        ArgGuard.Finite(End, "end");
    }

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static CubicBezier2D Create(Vector2 start, Vector2 control1, Vector2 control2, Vector2 end) => new CubicBezier2D(start, control1, control2, end).Validated();
    }
}
