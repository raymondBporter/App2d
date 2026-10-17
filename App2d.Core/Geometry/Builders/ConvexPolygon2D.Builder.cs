using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct ConvexPolygon2D
{
    private static void ValidateVertices(ReadOnlySpan<Vector2> vertices)
    {
        ArgGuard.ThrowIfTooShort(vertices, 3);
        for (var i = 0; i < vertices.Length; i++)
        {
            var current = vertices[i];
            var next = vertices[(i + 1) % vertices.Length];

            ArgGuard.Finite(current, nameof(vertices));
            if (Vector2.DistanceSquared(current, next) <= Epsilon * Epsilon)
            {
                ArgGuard.ThrowInvalid("Adjacent polygon vertices must be distinct.", nameof(vertices));
            }
        }
        if (!PolygonGeometry2D.IsConvexPerimeter(vertices, Epsilon))
        {
            ArgGuard.ThrowInvalid("Vertices must form a convex polygon in perimeter order with nonzero area.", nameof(vertices));
        }
    }
    public void Validate() => ValidateVertices(Vertices);

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static ConvexPolygon2D Create(IEnumerable<Vector2> vertices) => new ConvexPolygon2D([.. ArgGuard.RequireNotNull(vertices)]).Validated();
    }
}
