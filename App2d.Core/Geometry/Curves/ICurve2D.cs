using System.Numerics;

namespace App2d.Core.Geometry.Curves;

/// <summary>
/// A zero-thickness, one-dimensional path in 2D space, evaluated over normalized progress [0, 1].
/// Stroke width, caps, and joins belong to rendering; geometry queries describe the path itself.
/// </summary>
[System.Text.Json.Serialization.JsonConverter(typeof(Serialization.GeometryJsonConverterFactory))]
public interface ICurve2D : IGeometryValue2D, ICurve<Vector2, float>
{
    /// <summary>Returns the nonnegative distance from a point to the finite curve, including its endpoints.</summary>
    float Distance(Vector2 point);
}
