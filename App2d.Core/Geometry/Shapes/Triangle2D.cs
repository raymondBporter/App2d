using System.Numerics;

namespace App2d.Core.Geometry.Shapes;

/// <summary>A filled convex triangle in local space. Either perimeter winding is accepted.</summary>
public sealed class Triangle2D : IConvexShape2D
{
    private const double EdgeTolerance = 0.0001;
    private readonly bool _counterClockwise;

    public Triangle2D(Vector2 a, Vector2 b, Vector2 c)
    {
        ArgGuard.ThrowIfNotFinite(a);
        ArgGuard.ThrowIfNotFinite(b);
        ArgGuard.ThrowIfNotFinite(c);
        var signedAreaTwice = Cross((double)b.X - a.X, (double)b.Y - a.Y,
            (double)c.X - a.X, (double)c.Y - a.Y);
        if (signedAreaTwice == 0d)
            throw new ArgumentException("Triangle vertices must not be collinear.", nameof(c));

        var area = (float)(Math.Abs(signedAreaTwice) * 0.5);
        ArgGuard.ThrowIfNotFiniteOrNotPositive(area);
        A = a;
        B = b;
        C = c;
        Area = area;
        _counterClockwise = signedAreaTwice > 0d;
    }

    public Vector2 A { get; }
    public Vector2 B { get; }
    public Vector2 C { get; }
    public float Area { get; }

    public bool ContainsPoint(Vector2 localPoint)
    {
        ArgGuard.ThrowIfNotFinite(localPoint);
        var first = Side(A, B, localPoint);
        var second = Side(B, C, localPoint);
        var third = Side(C, A, localPoint);
        return _counterClockwise
            ? first >= -EdgeTolerance && second >= -EdgeTolerance && third >= -EdgeTolerance
            : first <= EdgeTolerance && second <= EdgeTolerance && third <= EdgeTolerance;
    }

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

    /// <summary>Writes A, B, C in perimeter order without allocating an array.</summary>
    public void WriteVertices(Span<Vector2> vertices)
    {
        ArgGuard.ThrowIfTooShort(vertices, 3);
        vertices[0] = A;
        vertices[1] = B;
        vertices[2] = C;
    }

    private static double Side(Vector2 start, Vector2 end, Vector2 point) =>
        Cross((double)end.X - start.X, (double)end.Y - start.Y,
            (double)point.X - start.X, (double)point.Y - start.Y);

    private static double Cross(double x, double y, double otherX, double otherY) =>
        x * otherY - y * otherX;

    private static double Dot(Vector2 first, Vector2 second) =>
        (double)first.X * second.X + (double)first.Y * second.Y;
}
