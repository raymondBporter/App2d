using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Curves;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct PolylineCurve2D
{
    public void Validate()
    {
        var points = Points;
        ArgGuard.ThrowIf(points.Length < 2, "A polyline needs at least two points.", "points");
        foreach (var point in points)
        {
            ArgGuard.Finite(point, "points");
        }
    }

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static PolylineCurve2D Create(IEnumerable<Vector2> points) => new PolylineCurve2D(points).Validated();
    }
}
