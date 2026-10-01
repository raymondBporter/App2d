using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A filled triangle in local space. Either perimeter winding is accepted; collinear vertices are rejected.</summary>
public sealed class Triangle2D : IConvexShape2D
{
    private const double EdgeTolerance = 0.0001;
    private readonly bool _counterClockwise;

    /// <summary>Creates a triangle from three finite, non-collinear vertices.</summary>
    /// <param name="a">The first vertex.</param>
    /// <param name="b">The second vertex.</param>
    /// <param name="c">The third vertex.</param>
    public Triangle2D(Vector2 a, Vector2 b, Vector2 c)
    {
        ArgGuard.ThrowIfNotFinite(a);
        ArgGuard.ThrowIfNotFinite(b);
        ArgGuard.ThrowIfNotFinite(c);
        var signedAreaTwice = CrossProduct2D.Orientation(a, b, c);
        if (signedAreaTwice == 0d) throw new ArgumentException("Triangle vertices must not be collinear.", nameof(c));

        var area = (float)(Math.Abs(signedAreaTwice) * 0.5);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(area);
        A = a;
        B = b;
        C = c;
        Area = area;
        _counterClockwise = signedAreaTwice > 0d;
    }

    /// <summary>The first vertex.</summary>
    public Vector2 A { get; }

    /// <summary>The second vertex.</summary>
    public Vector2 B { get; }

    /// <summary>The third vertex.</summary>
    public Vector2 C { get; }

    /// <inheritdoc/>
    public float Area { get; }

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2 localPoint)
    {
        ArgGuard.ThrowIfNotFinite(localPoint);
        var first = CrossProduct2D.Orientation(A, B, localPoint);
        var second = CrossProduct2D.Orientation(B, C, localPoint);
        var third = CrossProduct2D.Orientation(C, A, localPoint);
        return _counterClockwise
            ? first >= -EdgeTolerance && second >= -EdgeTolerance && third >= -EdgeTolerance
            : first <= EdgeTolerance && second <= EdgeTolerance && third <= EdgeTolerance;
    }

    /// <inheritdoc/>
    public Vector2 GetSupportPoint(Vector2 localDirection)
    {
        ArgGuard.ThrowIfNotFinite(localDirection);
        var best = A;
        var projection = Dot(A, localDirection);
        var next = Dot(B, localDirection);
        if (next > projection) { best = B; projection = next; }
        if (Dot(C, localDirection) > projection) best = C;
        return best;
    }

    /// <summary>Writes A, B and C in perimeter order without allocating.</summary>
    /// <param name="vertices">A buffer of at least three entries; extra entries are untouched.</param>
    public void WriteVertices(Span<Vector2> vertices)
    {
        ArgGuard.ThrowIfTooShort(vertices, 3);
        vertices[0] = A;
        vertices[1] = B;
        vertices[2] = C;
    }

    private static double Dot(Vector2 first, Vector2 second) => (double)first.X * second.X + (double)first.Y * second.Y;
}
