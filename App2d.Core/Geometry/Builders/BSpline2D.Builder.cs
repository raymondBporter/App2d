using App2d.Core.Geometry.Serialization;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Geometry.Curves;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct BSpline2D
{
    public void Validate()
    {
        ArgGuard.Positive(Degree, "degree");
        ArgGuard.ThrowIf(ControlPoints.Length <= Degree, $"A degree-{Degree} B-spline needs at least {(long)Degree + 1} control points.", "controlPoints");
        foreach (var point in ControlPoints) ArgGuard.Finite(point, "controlPoints");
    }
    public static class Builder
    {
        public static BSpline2D Create(IEnumerable<Vector2> controlPoints, int degree = 3)
        {
            return new BSpline2D(controlPoints, degree).Validated();
        }
    }
}
