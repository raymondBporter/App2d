using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Operations.Containment;
using App2d.Core.Geometry.Operations.Diameter;
using App2d.Core.Geometry.Operations.Polygons;
using App2d.Core.Geometry.Shapes;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace App2d.Core.Shapes;

/// <summary>A filled simple polygon, including concave outlines, in either winding.</summary>
public readonly partial record struct SimplePolygon2D : IShape2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.SimplePolygon;

    private readonly Vector2[]? _vertices;
    private static readonly ConditionalWeakTable<Vector2[], ReadOnlyCollection<Vector2>> Views = [];
    internal IReadOnlyList<Vector2> VertexList => Views.GetValue(_vertices!, static vertices => Array.AsReadOnly(vertices));

    /// <summary>Creates a polygon snapshot. Prefer Builder.Create for construction.</summary>
    public SimplePolygon2D(Vector2[] vertices) => _vertices = vertices?.ToArray();

    public ReadOnlySpan<Vector2> Vertices { get => _vertices; init => _vertices = value.ToArray(); }
    public float Area => Mesh.Area;

    /// <inheritdoc/>
    public float Diameter => Diameter2D.Polygon(_vertices);
    public bool ContainsPoint(Vector2 point) => Containment2D.SimplePolygon(point, Vertices);

    internal TriangleMesh2D Mesh => PolygonTriangulationCache2D.Get(VertexList, 1e-8);
    internal CompositeShape2D ConvexPieces => PolygonTriangulationCache2D.GetConvexPieces(VertexList, 1e-8);
}
