using App2d.Core.Geometry.Functions;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>An axis-aligned ellipse in local space; its object's pose can rotate it in world space.</summary>
public sealed class Ellipse2D : IConvexShape2D
{
    /// <summary>Perimeter samples used by the existing polygon-based distance and contact queries.</summary>
    public const int CollisionSegments = 64;

    public Ellipse2D(Vector2 radii, Vector2 center = default)
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(radii);
        ArgGuard.ThrowIfNotFinite(center);
        Radii = radii;
        Center = center;
    }

    public Vector2 Radii { get; }
    public Vector2 Center { get; }
    public float Area => PrimitiveGeometry2D.EllipseArea(Radii);

    public bool ContainsPoint(Vector2 localPoint) => PrimitiveGeometry2D.EllipseContainsPoint(localPoint, Center, Radii);

    public Vector2 GetSupportPoint(Vector2 localDirection) =>
        PrimitiveGeometry2D.EllipseSupportPoint(localDirection, Center, Radii);

    /// <summary>Writes a counter-clockwise perimeter; the buffer length chooses the segment count.</summary>
    public int WriteVertices(Span<Vector2> vertices) => VertexGenerator2D.WriteEllipse(vertices, Center, Radii);
}
