using App2d.Core.Geometry;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Operations.Polygons;
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

        /// <summary>
        /// Fills beneath a strictly left-to-right polyline down to a horizontal bottom. Builds a strip mesh
        /// directly, avoiding long triangulation diagonals and ear clipping for large terrain outlines.
        /// </summary>
        public static SimplePolygon2D BelowPolyline(ReadOnlySpan<Vector2> surface, float bottom)
        {
            ArgGuard.ThrowIfTooShort(surface, 2);
            ArgGuard.Finite(bottom);
            var count = surface.Length;
            var perimeter = new Vector2[count * 2];
            var indices = new int[(count - 1) * 6];
            for (var index = 0; index < count; index++)
            {
                ArgGuard.Finite(surface[index]);
                ArgGuard.ThrowIf(surface[index].Y <= bottom, "Terrain bottom must be below every surface point.");
                ArgGuard.ThrowIf(index > 0 && surface[index].X <= surface[index - 1].X, "Terrain must run strictly left to right.");
                perimeter[index] = surface[index];
                perimeter[2 * count - 1 - index] = new(surface[index].X, bottom);
                if (index == count - 1) continue;
                var offset = index * 6;
                var lowerLeft = 2 * count - 1 - index;
                var lowerRight = lowerLeft - 1;
                indices[offset] = index;
                indices[offset + 1] = index + 1;
                indices[offset + 2] = lowerRight;
                indices[offset + 3] = index;
                indices[offset + 4] = lowerRight;
                indices[offset + 5] = lowerLeft;
            }
            var polygon = new SimplePolygon2D(perimeter);
            PolygonTriangulationCache2D.StorePrepared(polygon.VertexList, new TriangleMesh2D(perimeter, indices), 1e-8);
            return polygon.Validated();
        }
    }
}
