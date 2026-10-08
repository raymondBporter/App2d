using App2d.Core.Meshes;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A filled simple polygon, including concave outlines, in either winding.</summary>
public sealed class SimplePolygon2D : IShape2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.SimplePolygon;

    private readonly Vector2[] _vertices;
    private readonly TriangleMesh2D _mesh;
    private readonly CompositeShape2D _pieces;

    public SimplePolygon2D(IEnumerable<Vector2> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        _vertices = [.. vertices];
        _mesh = TriangleMesh2D.TriangulateSimplePolygon(_vertices, 1e-8);
        _pieces = _mesh.ToCompositeShape();
    }

    public ReadOnlySpan<Vector2> Vertices => _vertices;
    public float Area => _mesh.Area;
    public bool ContainsPoint(Vector2 point) => _mesh.ContainsPoint(point);

    internal TriangleMesh2D Mesh => _mesh;
    internal CompositeShape2D ConvexPieces => _pieces;
}
