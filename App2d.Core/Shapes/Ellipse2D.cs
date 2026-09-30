using App2d.Core.Geometry;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>
/// A filled axis-aligned ellipse in local space; the owning pose can rotate it in world space.
/// Containment, support, bounds, ray casts, point distance and circle contacts are exact. Contacts and distances
/// against polygons, capsules and other ellipses use a <see cref="CollisionSegments"/>-gon perimeter instead.
/// </summary>
public sealed class Ellipse2D : IConvexShape2D
{
    /// <summary>Perimeter samples used where an ellipse is polygonized for contact and distance queries.</summary>
    public const int CollisionSegments = 64;

    /// <summary>Creates an ellipse.</summary>
    /// <param name="radii">The finite, positive half-extents along X and Y.</param>
    /// <param name="center">The finite local center.</param>
    public Ellipse2D(Vector2 radii, Vector2 center = default)
    {
        ArgGuard.ThrowIfNotFiniteOrNotPositive(radii);
        ArgGuard.ThrowIfNotFinite(center);
        Radii = radii;
        Center = center;
    }

    /// <summary>The half-extents along X and Y.</summary>
    public Vector2 Radii { get; }

    /// <summary>The local center.</summary>
    public Vector2 Center { get; }

    /// <inheritdoc/>
    public float Area => Area2D.Ellipse(Radii);

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2 localPoint) => Containment2D.Ellipse(localPoint, Center, Radii);

    /// <inheritdoc/>
    public Vector2 GetSupportPoint(Vector2 localDirection) => SupportPoint2D.Ellipse(localDirection, Center, Radii);

    /// <summary>Writes a counter-clockwise perimeter; the buffer length chooses the segment count.</summary>
    /// <param name="vertices">A buffer of at least three entries; every entry is written.</param>
    /// <returns>The number of vertices written.</returns>
    public int WriteVertices(Span<Vector2> vertices) => VertexGenerator2D.WriteEllipse(vertices, Center, Radii);
}
