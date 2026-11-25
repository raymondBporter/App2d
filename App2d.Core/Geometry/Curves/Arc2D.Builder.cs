using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Curves;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct Arc2D
{
    public void Validate()
    {
        Ensure.Finite(Center);
        Ensure.NonNegative(Radius);
        Ensure.Finite(StartAngleRadians);
        Ensure.Finite(SweepAngleRadians);
    }

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static Arc2D Create(Vector2f center, float radius, float startAngleRadians, float sweepAngleRadians)
        {
            return new Arc2D(center, radius, startAngleRadians, sweepAngleRadians).Validated();
        }
    }
}
