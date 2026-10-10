using App2d.Core.Meshes;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A filled simple polygon, including concave outlines, in either winding.</summary>
public sealed class SimplePolygon2D : IShape2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.SimplePolygon;

    private readonly Vector2[] _vertices;

    public SimplePolygon2D(IEnumerable<Vector2> vertices)
    {
        ArgumentNullException.ThrowIfNull(vertices);
        _vertices = [.. vertices];
        Mesh = TriangleMesh2D.TriangulateSimplePolygon(_vertices, 1e-8);
        ConvexPieces = Mesh.ToCompositeShape();
    }

    public ReadOnlySpan<Vector2> Vertices => _vertices;
    public float Area => Mesh.Area;
    public bool ContainsPoint(Vector2 point) => Mesh.ContainsPoint(point);

    internal TriangleMesh2D Mesh { get; }
    internal CompositeShape2D ConvexPieces { get; }
}
