using App2d.Core.Geometry.Operations.Diameter;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Validation;

namespace App2d.Core.Shapes;

/// <summary>A filled triangle in local space. Either perimeter winding is accepted; collinear vertices are rejected.</summary>
public readonly partial record struct Triangle2D : IConvexShape2D, ITriangle<Triangle2D, Vector2f>
{
    static Triangle2D ITriangle<Triangle2D, Vector2f>.Create(Vector2f a, Vector2f b, Vector2f c) => Builder.Create(a, b, c);

    private const double EdgeTolerance = 0.0001;
    private bool CounterClockwise => new Vector2d(A).Orientation(new(B), new(C)) > 0;

    /// <summary>Creates a triangle from three finite, non-collinear vertices.</summary>
    /// <param name="a">The first vertex.</param>
    /// <param name="b">The second vertex.</param>
    /// <param name="c">The third vertex.</param>
    public Triangle2D(Vector2f a, Vector2f b, Vector2f c)
    {
        A = a;
        B = b;
        C = c;
    }

    /// <summary>The first vertex.</summary>
    public Vector2f A { get; init; }

    /// <summary>The second vertex.</summary>
    public Vector2f B { get; init; }

    /// <summary>The third vertex.</summary>
    public Vector2f C { get; init; }

    /// <inheritdoc/>
    public float Area => (float)(Math.Abs(new Vector2d(A).Orientation(new(B), new(C))) * .5);

    /// <inheritdoc/>
    public float Diameter => Diameter2D.Triangle(A, B, C);

    /// <inheritdoc/>
    public bool ContainsPoint(Vector2f localPoint) =>
        TriangleMath2D.Contains(this, localPoint, EdgeTolerance, CounterClockwise ? 1 : -1);

    /// <inheritdoc/>
    public Vector2f GetSupportPoint(Vector2f localDirection)
    {
        Ensure.Finite(localDirection);
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
    public void WriteVertices(Span<Vector2f> vertices) => TriangleMath2D.WriteVertices(this, vertices);
}
