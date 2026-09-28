using App2d.Core.Geometry.Functions;
using App2d.Core.Validation;
using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Core.Shapes;

public sealed class ConvexPolygon2D : IConvexShape2D
{
    private const float Epsilon = 0.0001f;
    private readonly Vector2[] _vertices;

    public ConvexPolygon2D(IEnumerable<Vector2> vertices)
    {
        _vertices = [.. vertices];
        Validate(_vertices);
        Area = PolygonGeometry2D.Area(_vertices);
    }

    public ReadOnlySpan<Vector2> Vertices => _vertices;
    public float Area { get; }

    public bool ContainsPoint(Vector2 localPoint) => PolygonGeometry2D.ContainsPoint(_vertices, localPoint, Epsilon);

    public Vector2 GetSupportPoint(Vector2 localDirection) => PolygonGeometry2D.GetSupportPoint(_vertices, localDirection);

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
            if (Vector2.DistanceSquared(current, next) <= Epsilon * Epsilon)
                ArgGuard.ThrowInvalid("Adjacent polygon vertices must be distinct.", nameof(vertices));

            var cross = CrossProduct2D.Orientation(current, next, afterNext);
            if (Math.Abs(cross) <= Epsilon)
                continue;

            var turn = Math.Sign(cross);
            if (winding == 0)
                winding = turn;
            else if (turn != winding)
                ArgGuard.ThrowInvalid("Vertices must form a convex polygon in perimeter order.", nameof(vertices));
        }

        if (winding == 0f)
            ArgGuard.ThrowInvalid("Polygon vertices cannot all be collinear.", nameof(vertices));
    }

}
