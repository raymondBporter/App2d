using App2d.Core.Geometry;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Curves;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct LineSegmentCurve2D
{
    public void Validate()
    {
        ArgGuard.Finite(Start, "start");
        ArgGuard.Finite(End, "end");
    }

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static LineSegmentCurve2D Create(Vector2 start, Vector2 end) => new LineSegmentCurve2D(start, end).Validated();
    }
}
