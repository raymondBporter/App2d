using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct Triangle2D
{
    public void Validate()
    {
        Ensure.Finite(A, "a");
        Ensure.Finite(B, "b");
        Ensure.Finite(C, "c");
        var twice = this.SignedAreaTwice();
        Ensure.NonZero(twice);

        Ensure.Positive((float)(Math.Abs(twice) * .5), "area");
    }

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static Triangle2D Create(Vector2f a, Vector2f b, Vector2f c) => new Triangle2D(a, b, c).Validated();
    }
}
