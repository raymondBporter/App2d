using App2d.Core.Geometry.Serialization;
using System.Text.Json.Serialization;

namespace App2d.Core.Geometry;

/// <summary>A shape or curve value stored directly in an authored geometry field.</summary>
[JsonConverter(typeof(GeometryJsonConverterFactory))]
public interface IGeometryValue2D
{
    string Kind => GetType().Name;

    /// <summary>Checks this value. Custom implementations can supply their own rules.</summary>
    void Validate() { }
}
