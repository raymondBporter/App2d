using App2d.Core.Geometry.Containment;
using App2d.Core.Geometry.Measurements;
using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Meshes.Tessellation;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;

namespace App2d.Core.Geometry.Shapes;

/// <summary>A filled simple polygon, including concave outlines, in either winding.</summary>
public readonly partial record struct SimplePolygon2D : IShape2D, IInterpolatable<SimplePolygon2D, float>
{
    public GeometryKind Kind => GeometryKind.SimplePolygon;

    /// <summary>
    /// Moves each corner in a straight line to the corner with the same number, so both polygons need the same number of
    /// corners. Nothing checks the shape on the way: corners that cross each other give an outline that crosses itself.
    /// </summary>
    public static SimplePolygon2D Lerp(SimplePolygon2D start, SimplePolygon2D end, float amount)
    {
        var from = start.Vertices;
        var to = end.Vertices;
        if (from.Length != to.Length) throw new ArgumentException($"Polygons of {from.Length} and {to.Length} corners cannot be interpolated corner to corner.", nameof(end));
        var corners = new Vector2f[from.Length];
        for (var i = 0; i < corners.Length; i++) corners[i] = Vector2f.Lerp(from[i], to[i], amount);
        return new(corners);
    }

    private readonly Vector2f[]? _vertices;
    private static readonly ConditionalWeakTable<Vector2f[], ReadOnlyCollection<Vector2f>> Views = [];
    internal IReadOnlyList<Vector2f> VertexList => Views.GetValue(_vertices!, static vertices => Array.AsReadOnly(vertices));

    /// <summary>Creates a polygon snapshot. Prefer Builder.Create for construction.</summary>
    public SimplePolygon2D(Vector2f[] vertices) => _vertices = vertices?.ToArray();

    public ReadOnlySpan<Vector2f> Vertices { get => _vertices; init => _vertices = value.ToArray(); }
    public float Area => Mesh.Area;

    /// <inheritdoc/>
    public float Diameter => Diameter2D.Polygon(_vertices);
    public bool ContainsPoint(Vector2f point) => Containment2D.SimplePolygon(point, Vertices);

    internal TriangleMesh2D Mesh => PolygonTriangulationCache2D.Get(VertexList, 1e-8);
    internal CompositeShape2D ConvexPieces => PolygonTriangulationCache2D.GetConvexPieces(VertexList, 1e-8);
}
