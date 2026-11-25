using App2d.Core.Geometry.Serialization;
using System.Text.Json.Serialization;

namespace App2d.Core.Geometry;

/// <summary>A shape or curve value stored directly in an authored geometry field.</summary>
[JsonConverter(typeof(GeometryJsonConverterFactory))]
public interface IGeometryValue2D
{
    /// <summary>The stable kind ID used by editors and polymorphic JSON.</summary>
    GeometryKind Kind { get; }

    /// <summary>Checks this value. Custom implementations can supply their own rules.</summary>
    void Validate() { }
}
