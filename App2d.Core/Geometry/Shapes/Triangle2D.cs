using App2d.Core.Geometry.Operations.Diameter;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

/// <summary>A filled triangle in local space. Either perimeter winding is accepted; collinear vertices are rejected.</summary>
public readonly partial record struct Triangle2D : IConvexShape2D, ITriangle<Triangle2D, Vector2>
{
    /// <inheritdoc/>
    public string Kind => ShapeKinds2D.Triangle;

    static Triangle2D ITriangle<Triangle2D, Vector2>.Create(Vector2 a, Vector2 b, Vector2 c) => Builder.Create(a, b, c);

    private const double EdgeTolerance = 0.0001;
    private bool CounterClockwise => new Vector2d(A).Orientation(new(B), new(C)) > 0;

    /// <summary>Creates a triangle from three finite, non-collinear vertices.</summary>
    /// <param name="a">The first vertex.</param>
    /// <param name="b">The second vertex.</param>
    /// <param name="c">The third vertex.</param>
    public Triangle2D(Vector2 a, Vector2 b, Vector2 c)
    {
        A = a;
        B = b;
        C = c;
    }

    /// <summary>The first vertex.</summary>
    public Vector2 A
    {
        get; init;
    }

    /// <summary>The second vertex.</summary>
    public Vector2 B
    {
        get; init;
    }

    /// <summary>The third vertex.</summary>
    public Vector2 C
    {
        get; init;
    }

    /// <inheritdoc/>
    public float Area => (float)(Math.Abs(new Vector2d(A).Orientation(new(B), new(C))) * .5);

    /// <inheritdoc/>
    public float Diameter => Diameter2D.Triangle(A, B, C);

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2 localPoint) =>
        TriangleMath2D.Contains(this, localPoint, EdgeTolerance, CounterClockwise ? 1 : -1);

    /// <inheritdoc/>
    public Vector2 GetSupportPoint(Vector2 localDirection)
    {
        ArgGuard.Finite(localDirection);
        var direction = localDirection.ToVector2d();
        var best = A;
        var projection = A.ToVector2d().Dot(direction);
        var next = B.ToVector2d().Dot(direction);
        if (next > projection)
        {
            best = B;
            projection = next;
        }
        if (C.ToVector2d().Dot(direction) > projection)
        {
            best = C;
        }

        return best;
    }

    /// <summary>Writes A, B and C in perimeter order without allocating.</summary>
    /// <param name="vertices">A buffer of at least three entries; extra entries are untouched.</param>
    public void WriteVertices(Span<Vector2> vertices) => TriangleMath2D.WriteVertices(this, vertices);
}
