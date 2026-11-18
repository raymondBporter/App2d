using App2d.Core.Geometry;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;

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
        public static Triangle2D Create(Vector2f a, Vector2f b, Vector2f c) => new Triangle2D(a, b, c).Validated();
    }
}
