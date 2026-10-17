using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Curves;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct Arc2D
{
    public void Validate()
    {
        ArgGuard.Finite(Center, "center");
        ArgGuard.NonNegative(Radius, "radius");
        ArgGuard.Finite(StartAngleRadians, "startAngleRadians");
        ArgGuard.Finite(SweepAngleRadians, "sweepAngleRadians");
    }

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static Arc2D Create(Vector2 center, float radius, float startAngleRadians, float sweepAngleRadians) => new Arc2D(center, radius, startAngleRadians, sweepAngleRadians).Validated();
    }
}
