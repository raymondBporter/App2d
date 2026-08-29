using System.Numerics;
using App2d.Engine.Mathematics;

namespace App2d.Engine.Geometry;

public sealed class ConvexPolygon2D : IConvexShape2D
{
    private const float Epsilon = 0.0001f;
    private readonly Vector2[] _vertices;

    public ConvexPolygon2D(IEnumerable<Vector2> vertices)
    {
        _vertices = [.. vertices];
        Validate(_vertices);
        LocalBounds = Bounds2D.FromPoints(_vertices);
    }

    public ReadOnlySpan<Vector2> Vertices => _vertices;
    public Bounds2D LocalBounds { get; }

    public bool ContainsPoint(Vector2 localPoint)
    {
        var winding = 0f;
        for (var i = 0; i < _vertices.Length; i++)
        {
            var start = _vertices[i];
            var end = _vertices[(i + 1) % _vertices.Length];
            var cross = (end - start).Cross(localPoint - start);
            if (MathF.Abs(cross) <= Epsilon)
                continue;

            float turn = MathF.Sign(cross);
            if (winding == 0f)
                winding = turn;
            else if (turn != winding)
                return false;
        }

        return true;
    }

    public Vector2 GetSupportPoint(Vector2 localDirection)
    {
        var support = _vertices[0];
        var bestProjection = Vector2.Dot(support, localDirection);
        foreach (var vertex in _vertices.AsSpan(1))
        {
            var projection = Vector2.Dot(vertex, localDirection);
            if (projection > bestProjection)
            {
                support = vertex;
                bestProjection = projection;
            }
        }

        return support;
    }

    private static void Validate(ReadOnlySpan<Vector2> vertices)
    {
        ArgGuard.ThrowIfTooShort(vertices, 3);

        var winding = 0f;
        for (var i = 0; i < vertices.Length; i++)
        {
            var current = vertices[i];
            var next = vertices[(i + 1) % vertices.Length];
            var afterNext = vertices[(i + 2) % vertices.Length];

            ArgGuard.ThrowIfNotFinite(current, nameof(vertices));
            if (Vector2.DistanceSquared(current, next) <= Epsilon * Epsilon)
                ArgGuard.ThrowInvalid("Adjacent polygon vertices must be distinct.", nameof(vertices));

            var cross = (next - current).Cross(afterNext - next);
            if (MathF.Abs(cross) <= Epsilon)
                continue;

            float turn = MathF.Sign(cross);
            if (winding == 0f)
                winding = turn;
            else if (turn != winding)
                ArgGuard.ThrowInvalid("Vertices must form a convex polygon in perimeter order.", nameof(vertices));
        }

        if (winding == 0f)
            ArgGuard.ThrowInvalid("Polygon vertices cannot all be collinear.", nameof(vertices));
    }

}
