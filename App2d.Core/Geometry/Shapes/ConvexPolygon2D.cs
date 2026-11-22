using App2d.Core.Geometry.Containment;
using App2d.Core.Geometry.Convex;
using App2d.Core.Geometry.Measurements;

namespace App2d.Core.Geometry.Shapes;

/// <summary>A filled convex polygon in local space, given in perimeter order in either winding.</summary>
public readonly partial record struct ConvexPolygon2D : IConvexShape2D
{
    public const string Kind = "convex-polygon";

    private const float Epsilon = 0.0001f;
    private readonly Vector2f[]? _vertices;

    /// <summary>Creates a convex polygon and copies its vertices.</summary>
    /// <param name="vertices">At least three finite vertices in perimeter order, with distinct neighbors and consistent turning.</param>
    public ConvexPolygon2D(Vector2f[] vertices)
    {
        _vertices = vertices?.ToArray();
    }

    /// <summary>The perimeter vertices.</summary>
    public ReadOnlySpan<Vector2f> Vertices { get => _vertices; init => _vertices = value.ToArray(); }

    /// <inheritdoc/>
    public float Area => Area2D.Polygon(_vertices);

    /// <inheritdoc/>
    public float Diameter => Diameter2D.Polygon(_vertices);

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2f localPoint) => Containment2D.ConvexPolygon(localPoint, _vertices, Epsilon);

    /// <inheritdoc/>
    public Vector2f GetSupportPoint(Vector2f localDirection) => SupportPoint2D.Polygon(localDirection, _vertices);

}
