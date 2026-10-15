using App2d.Core.Geometry.Operations.Diameter;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A filled triangle in local space. Either perimeter winding is accepted; collinear vertices are rejected.</summary>
public sealed partial class Triangle2D : IConvexShape2D
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.Triangle;

    private const double EdgeTolerance = 0.0001;
    private readonly bool _counterClockwise;

    /// <summary>Creates a triangle from three finite, non-collinear vertices.</summary>
    /// <param name="a">The first vertex.</param>
    /// <param name="b">The second vertex.</param>
    /// <param name="c">The third vertex.</param>
    private Triangle2D(Vector2 a, Vector2 b, Vector2 c, double signedAreaTwice)
    {
        A = a;
        B = b;
        C = c;
        Area = (float)(Math.Abs(signedAreaTwice) * .5);
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
    public float Diameter => Diameter2D.Triangle(A, B, C);

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2 localPoint)
    {
        ArgGuard.ThrowIfNotFinite(localPoint);
        var first = A.Orientation(B, localPoint);
        var second = B.Orientation(C, localPoint);
        var third = C.Orientation(A, localPoint);
        return _counterClockwise
            ? first >= -EdgeTolerance && second >= -EdgeTolerance && third >= -EdgeTolerance
            : first <= EdgeTolerance && second <= EdgeTolerance && third <= EdgeTolerance;
    }

    /// <inheritdoc/>
    public Vector2 GetSupportPoint(Vector2 localDirection)
    {
        ArgGuard.ThrowIfNotFinite(localDirection);
        var direction = localDirection.ToVector2d();
        var best = A;
        var projection = A.ToVector2d().Dot(direction);
        var next = B.ToVector2d().Dot(direction);
        if (next > projection) { best = B; projection = next; }
        if (C.ToVector2d().Dot(direction) > projection) best = C;
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
}
