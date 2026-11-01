using App2d.Core.Geometry;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct Triangle2D
{
    private double SignedAreaTwice()
    {
        Ensure.Finite(A, "a");
        Ensure.Finite(B, "b");
        Ensure.Finite(C, "c");
        var twice = new Vector2d(A).Orientation(new(B), new(C));
        Ensure.NonZero(twice);

        Ensure.Positive((float)(Math.Abs(twice) * .5), "area");
        return twice;
    }
    public void Validate() => SignedAreaTwice();

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static Triangle2D Create(Vector2 a, Vector2 b, Vector2 c) => new Triangle2D(a, b, c).Validated();
    }
}
