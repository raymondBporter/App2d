using App2d.Core.Geometry;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct SimplePolygon2D
{
    public void Validate() => _ = Mesh;

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static SimplePolygon2D Create(IEnumerable<Vector2> vertices) => new SimplePolygon2D([.. ArgGuard.RequireNotNull(vertices)]).Validated();
    }
}
