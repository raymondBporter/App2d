using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Curves;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct PolylineCurve2D
{
    public void Validate()
    {
        var points = Points;
        Ensure.ThrowIf(points.Length < 2, "A polyline needs at least two points.", "points");
        foreach (var point in points)
        {
            Ensure.Finite(point, "points");
        }
    }

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static PolylineCurve2D Create(IEnumerable<Vector2f> points) => new PolylineCurve2D(points).Validated();

        internal static PolylineCurve2D CreateFromSpan(ReadOnlySpan<Vector2f> points)
        {
            var polyline = new PolylineCurve2D { Points = points };
            polyline.Validate();
            return polyline;
        }
    }
}
