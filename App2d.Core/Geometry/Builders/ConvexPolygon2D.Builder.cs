using App2d.Core.Geometry.Builders;
using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Shapes;

public sealed partial class ConvexPolygon2D
{
    /// <summary>Editable construction data; Build validates before creating an immutable shape.</summary>
    public sealed record Builder : ConvexShapeBuilder2D
    {
        public required List<Vector2> Vertices
        {
            get; init;
        }
        [JsonIgnore] public override string Kind => ShapeKinds2D.ConvexPolygon;
        public override void Validate() => ValidateVertices(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(ArgGuard.RequireNotNull(Vertices, "vertices")));
        public override ConvexPolygon2D Build()
        {
            Vector2[] vertices = [.. ArgGuard.RequireNotNull(Vertices, "vertices")];
            ValidateVertices(vertices);
            return new(vertices);
        }
        public static ConvexPolygon2D Create(IEnumerable<Vector2> vertices) => new Builder { Vertices = [.. ArgGuard.RequireNotNull(vertices)] }.Build();
        private static void ValidateVertices(ReadOnlySpan<Vector2> vertices)
        {
            ArgGuard.ThrowIfTooShort(vertices, 3);
            for (var i = 0; i < vertices.Length; i++)
            {
                var current = vertices[i];
                var next = vertices[(i + 1) % vertices.Length];

                ArgGuard.ThrowIfNotFinite(current, nameof(vertices));
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
    }
}
