using App2d.Core.Geometry;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A filled convex polygon in local space, given in perimeter order in either winding.</summary>
public sealed class ConvexPolygon2D : IConvexShape2D
{
    private const float Epsilon = 0.0001f;
    private readonly Vector2[] _vertices;

    /// <summary>Creates a convex polygon and copies its vertices.</summary>
    /// <param name="vertices">At least three finite vertices in perimeter order, with distinct neighbors and consistent turning.</param>
    public ConvexPolygon2D(IEnumerable<Vector2> vertices)
    {
        _vertices = [.. vertices];
        Validate(_vertices);
        Area = Area2D.Polygon(_vertices);
    }

    /// <summary>The perimeter vertices.</summary>
    public ReadOnlySpan<Vector2> Vertices => _vertices;

    /// <inheritdoc/>
    public float Area { get; }

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2 localPoint) => Containment2D.ConvexPolygon(localPoint, _vertices, Epsilon);

    /// <inheritdoc/>
    public Vector2 GetSupportPoint(Vector2 localDirection) => SupportPoint2D.Polygon(localDirection, _vertices);

    private static void Validate(ReadOnlySpan<Vector2> vertices)
    {
        ArgGuard.ThrowIfTooShort(vertices, 3);
        var winding = 0;
        for (var i = 0; i < vertices.Length; i++)
        {
            var current = vertices[i];
            var next = vertices[(i + 1) % vertices.Length];
            var afterNext = vertices[(i + 2) % vertices.Length];

            ArgGuard.ThrowIfNotFinite(current, nameof(vertices));
            if (Vector2.DistanceSquared(current, next) <= Epsilon * Epsilon) ArgGuard.ThrowInvalid("Adjacent polygon vertices must be distinct.", nameof(vertices));

            var cross = CrossProduct2D.Orientation(current, next, afterNext);
            if (Math.Abs(cross) <= Epsilon) continue;

            var turn = Math.Sign(cross);
            if (winding == 0) winding = turn;
            else if (turn != winding) ArgGuard.ThrowInvalid("Vertices must form a convex polygon in perimeter order.", nameof(vertices));
        }

        if (winding == 0) ArgGuard.ThrowInvalid("Polygon vertices cannot all be collinear.", nameof(vertices));
    }
}
