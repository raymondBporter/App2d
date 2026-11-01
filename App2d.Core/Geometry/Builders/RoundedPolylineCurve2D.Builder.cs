using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Curves;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct RoundedPolylineCurve2D
{
    public void Validate()
    {
        var points = Points;
        Ensure.ThrowIf(points.Length < 2, "A rounded polyline needs at least two points.", "points");
        foreach (var point in points)
        {
            Ensure.Finite(point, "points");
        }

        Ensure.InRange(Softness, 0f, 1f, "softness");
    }

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static RoundedPolylineCurve2D Create(IEnumerable<Vector2> points, float softness = 1) => new RoundedPolylineCurve2D(points, softness).Validated();
    }
}
