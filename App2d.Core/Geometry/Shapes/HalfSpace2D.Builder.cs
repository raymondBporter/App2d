using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;

namespace App2d.Core.Geometry.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct HalfSpace2D
{
    public void Validate()
    {
        Ensure.NonZero(Normal, "outwardNormal");
        Ensure.Finite(Offset, "offset");
        Ensure.LessThanOrEqual(Math.Abs(Normal.ToVector2d().Length() - 1), 1e-6, "outwardNormal");
    }

    public static class Builder
    {
        public static HalfSpace2D Create(Vector2f outwardNormal, float offset)
        {
            Ensure.NonZero(outwardNormal);
            Ensure.Finite(offset);
            var wide = outwardNormal.ToVector2d();
            var length = wide.Length();
            return new HalfSpace2D((wide / length).ToVector2f(), (float)(offset / length)).Validated();
        }

        public static HalfSpace2D FromPoint(Vector2f pointOnBoundary, Vector2f outwardNormal)
        {
            Ensure.Finite(pointOnBoundary);
            Ensure.NonZero(outwardNormal);
            var wide = outwardNormal.ToVector2d();
            var normal = (wide / wide.Length()).ToVector2f();
            return Create(normal, (float)pointOnBoundary.ToVector2d().Dot(normal.ToVector2d()));
        }
        public static HalfSpace2D CreateFromPoint(Vector2f pointOnBoundary, Vector2f outwardNormal) => FromPoint(pointOnBoundary, outwardNormal);
    }
}
