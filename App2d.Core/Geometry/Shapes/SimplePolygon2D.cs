using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Operations.Conversion;
using App2d.Core.Geometry.Operations.Diameter;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Geometry.Operations.Polygons;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A filled simple polygon, including concave outlines, in either winding.</summary>
public readonly partial record struct SimplePolygon2D : IShape2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.SimplePolygon;

    private readonly Vector2[] _vertices;

    /// <summary>Creates a polygon snapshot. Prefer Builder.Create for construction.</summary>
    public SimplePolygon2D(Vector2[] vertices)
        : this((Vector2[])vertices.Clone(), PolygonTriangulation2D.Triangulate(vertices, 1e-8)) { }

    private SimplePolygon2D(Vector2[] vertices, TriangleMesh2D mesh)
    {
        _vertices = vertices;
        Mesh = mesh;
        ConvexPieces = Mesh.ToCompositeShape();
    }

    public ReadOnlySpan<Vector2> Vertices => _vertices;
    public float Area => Mesh.Area;

    /// <inheritdoc/>
    public float Diameter => Diameter2D.Polygon(_vertices);
    public bool ContainsPoint(Vector2 point) => Mesh.ContainsPoint(point);

    internal TriangleMesh2D Mesh { get; }
    internal CompositeShape2D ConvexPieces { get; }
}
