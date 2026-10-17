using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Containment;
using App2d.Core.Geometry.Operations.Convex;
using App2d.Core.Geometry.Operations.Diameter;
using App2d.Core.Geometry.Shapes;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A filled convex polygon in local space, given in perimeter order in either winding.</summary>
public readonly partial record struct ConvexPolygon2D : IConvexShape2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.ConvexPolygon;

    private const float Epsilon = 0.0001f;
    private readonly Vector2[] _vertices;

    /// <summary>Creates a convex polygon and copies its vertices.</summary>
    /// <param name="vertices">At least three finite vertices in perimeter order, with distinct neighbors and consistent turning.</param>
    public ConvexPolygon2D(Vector2[] vertices)
    {
        _vertices = (Vector2[])vertices.Clone();
        Area = Area2D.Polygon(_vertices);
    }

    /// <summary>The perimeter vertices.</summary>
    public ReadOnlySpan<Vector2> Vertices => _vertices;

    /// <inheritdoc/>
    public float Area
    {
        get;
    }

    /// <inheritdoc/>
    public float Diameter => Diameter2D.Polygon(_vertices);

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2 localPoint) => Containment2D.ConvexPolygon(localPoint, _vertices, Epsilon);

    /// <inheritdoc/>
    public Vector2 GetSupportPoint(Vector2 localDirection) => SupportPoint2D.Polygon(localDirection, _vertices);

}
