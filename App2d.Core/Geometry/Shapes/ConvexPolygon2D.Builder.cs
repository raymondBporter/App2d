using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct ConvexPolygon2D
{
    private static void ValidateVertices(ReadOnlySpan<Vector2f> vertices)
    {
        Ensure.ThrowIfTooShort(vertices, 3);
        for (var i = 0; i < vertices.Length; i++)
        {
            var current = vertices[i];
            var next = vertices[(i + 1) % vertices.Length];

            Ensure.Finite(current, nameof(vertices));
            if (Vector2f.DistanceSquared(current, next) <= Epsilon * Epsilon)
            {
                Ensure.ThrowInvalid("Adjacent polygon vertices must be distinct.", nameof(vertices));
            }
        }
        if (!PolygonGeometry2D.IsConvexPerimeter(vertices, Epsilon))
        {
            Ensure.ThrowInvalid("Vertices must form a convex polygon in perimeter order with nonzero area.", nameof(vertices));
        }
    }
    public void Validate() => ValidateVertices(Vertices);

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static ConvexPolygon2D Create(IEnumerable<Vector2f> vertices) => new ConvexPolygon2D([.. Ensure.RequireNotNull(vertices)]).Validated();

        /// <summary>Creates a regular hexagon with the given center-to-corner radius and rotation in radians.</summary>
        public static ConvexPolygon2D CreateRegularHexagon(float radius, Vector2f center = default, float angleRadians = 0f)
        {
            Span<Vector2f> vertices = stackalloc Vector2f[6];
            VertexGenerator2D.WriteRegularHexagon(vertices, center, radius, angleRadians);
            return new ConvexPolygon2D { Vertices = vertices }.Validated();
        }
    }
}
