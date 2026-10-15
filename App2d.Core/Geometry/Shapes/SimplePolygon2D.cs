using App2d.Core.Geometry;
using App2d.Core.Meshes;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A filled simple polygon, including concave outlines, in either winding.</summary>
public sealed partial class SimplePolygon2D : IShape2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.SimplePolygon;

    private readonly Vector2[] _vertices;

    private SimplePolygon2D(Vector2[] vertices, TriangleMesh2D mesh)
    {
        _vertices = vertices;
        Mesh = mesh;
        ConvexPieces = Mesh.ToCompositeShape();
    }

    public ReadOnlySpan<Vector2> Vertices => _vertices;
    public float Area => Mesh.Area;
    public bool ContainsPoint(Vector2 point) => Mesh.ContainsPoint(point);

    internal TriangleMesh2D Mesh { get; }
    internal CompositeShape2D ConvexPieces { get; }
}
