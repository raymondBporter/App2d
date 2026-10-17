using App2d.Core.Geometry.Builders;
using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Validation;
using System.Numerics;
using System.Text.Json.Serialization;

namespace App2d.Core.Shapes;

public readonly partial record struct SimplePolygon2D
{
    /// <summary>Editable construction data; Build validates before creating an immutable shape.</summary>
    public sealed record Builder : ShapeBuilder2D
    {
        public required List<Vector2> Vertices { get; init; }
        [JsonIgnore] public override string Kind => ShapeKinds2D.SimplePolygon;

        public override void Validate() => PolygonTriangulation2D.Triangulate(ArgGuard.RequireNotNull(Vertices, "vertices"), 1e-8);

        protected override IShape2D BuildShape() => Build();

        public new SimplePolygon2D Build()
        {
            Vector2[] vertices = [.. ArgGuard.RequireNotNull(Vertices, "vertices")];
            var mesh = PolygonTriangulation2D.Triangulate(vertices, 1e-8);
            return new(vertices, mesh);
        }

        public static SimplePolygon2D Create(IEnumerable<Vector2> vertices) => new Builder { Vertices = [.. ArgGuard.RequireNotNull(vertices)] }.Build();
    }
}
