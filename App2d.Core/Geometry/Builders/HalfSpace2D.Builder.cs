using App2d.Core.Geometry;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct HalfSpace2D
{
    public void Validate()
    {
        ArgGuard.NonZero(Normal, "outwardNormal");
        ArgGuard.Finite(Offset, "offset");
        ArgGuard.ThrowIf(Math.Abs(Normal.ToVector2d().Length() - 1) > 1e-6, "The half-space normal must be normalized; use Builder.Create.", "outwardNormal");
    }

    public static class Builder
    {
        public static HalfSpace2D Create(Vector2 outwardNormal, float offset)
        {
            ArgGuard.NonZero(outwardNormal);
            ArgGuard.Finite(offset);
            var wide = outwardNormal.ToVector2d();
            var length = wide.Length();
            return new HalfSpace2D((wide / length).ToVector2(), (float)(offset / length)).Validated();
        }

        public static HalfSpace2D FromPoint(Vector2 pointOnBoundary, Vector2 outwardNormal)
        {
            ArgGuard.Finite(pointOnBoundary);
            ArgGuard.NonZero(outwardNormal);
            var wide = outwardNormal.ToVector2d();
            var normal = (wide / wide.Length()).ToVector2();
            return Create(normal, (float)pointOnBoundary.ToVector2d().Dot(normal.ToVector2d()));
        }
        public static HalfSpace2D CreateFromPoint(Vector2 pointOnBoundary, Vector2 outwardNormal) => FromPoint(pointOnBoundary, outwardNormal);
    }
}
