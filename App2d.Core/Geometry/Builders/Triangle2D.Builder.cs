using App2d.Core.Geometry;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct Triangle2D
{
    private double SignedAreaTwice()
    {
        ArgGuard.Finite(A, "a");
        ArgGuard.Finite(B, "b");
        ArgGuard.Finite(C, "c");
        var twice = new Vector2d(A).Orientation(new(B), new(C));
        if (twice == 0d)
        {
            throw new ArgumentException("Triangle vertices must not be collinear.", "c");
        }

        ArgGuard.Positive((float)(Math.Abs(twice) * .5), "area");
        return twice;
    }
    public void Validate() => SignedAreaTwice();

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static Triangle2D Create(Vector2 a, Vector2 b, Vector2 c) => new Triangle2D(a, b, c).Validated();
    }
}
