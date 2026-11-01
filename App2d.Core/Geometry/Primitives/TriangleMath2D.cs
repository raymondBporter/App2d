using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Primitives;

/// <summary>Operations shared by triangle values and validated triangle shapes, without boxing.</summary>
public static class TriangleMath2D
{
    public static double SignedAreaTwice<TTriangle>(this TTriangle triangle)
        where TTriangle : ITriangle<TTriangle, Vector2> =>
        new Vector2d(triangle.A).Orientation(new(triangle.B), new(triangle.C));

    public static double GetArea<TTriangle>(this TTriangle triangle)
        where TTriangle : ITriangle<TTriangle, Vector2> => Math.Abs(triangle.SignedAreaTwice()) * .5d;

    public static bool Contains<TTriangle>(this TTriangle triangle, Vector2 point, double edgeTolerance = 0)
        where TTriangle : ITriangle<TTriangle, Vector2>
        => Contains(triangle, point, edgeTolerance, Math.Sign(triangle.SignedAreaTwice()));

    // Validated shapes already cache winding; share the predicate without recalculating it for each query.
    internal static bool Contains<TTriangle>(TTriangle triangle, Vector2 point, double edgeTolerance, int winding)
        where TTriangle : ITriangle<TTriangle, Vector2>
    {
        Ensure.Finite(point);
        Ensure.NonNegative(edgeTolerance);
        var a = new Vector2d(triangle.A);
        var b = new Vector2d(triangle.B);
        var c = new Vector2d(triangle.C);
        var p = new Vector2d(point);
        return winding != 0 && winding * a.Orientation(b, p) >= -edgeTolerance &&
            winding * b.Orientation(c, p) >= -edgeTolerance && winding * c.Orientation(a, p) >= -edgeTolerance;
    }

    /// <summary>
    /// Weights of <paramref name="a"/>, <paramref name="b"/> and <paramref name="c"/> that rebuild <paramref name="point"/>; they sum to 1 and
    /// interpolate any per-corner value. Points outside the triangle get a negative weight. A degenerate triangle gives non-finite weights.
    /// </summary>
    public static Vector3 Barycentric(Vector2 a, Vector2 b, Vector2 c, Vector2 point)
    {
        var ab = b - a;
        var ac = c - a;
        var determinant = ab.Cross(ac);
        var d = point - a;
        var u = d.Cross(ac) / determinant;
        var v = ab.Cross(d) / determinant;
        return new(1 - u - v, u, v);
    }

    public static TTriangle Reversed<TTriangle>(this TTriangle triangle)
        where TTriangle : ITriangle<TTriangle, Vector2> => TTriangle.Create(triangle.A, triangle.C, triangle.B);

    public static TTriangle TransformedBy<TTriangle>(this TTriangle triangle, Matrix3x2 transform)
        where TTriangle : ITriangle<TTriangle, Vector2> => TTriangle.Create(
        Vector2.Transform(triangle.A, transform), Vector2.Transform(triangle.B, transform), Vector2.Transform(triangle.C, transform));

    public static void WriteVertices<TTriangle>(this TTriangle triangle, Span<Vector2> vertices)
        where TTriangle : ITriangle<TTriangle, Vector2>
    {
        Ensure.ThrowIfTooShort(vertices, 3);
        vertices[0] = triangle.A;
        vertices[1] = triangle.B;
        vertices[2] = triangle.C;
    }
}
