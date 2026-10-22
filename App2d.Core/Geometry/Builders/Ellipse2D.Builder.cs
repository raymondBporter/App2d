using App2d.Core.Geometry;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Mathematics;
using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Shapes;

[System.Text.Json.Serialization.JsonConverter(typeof(GeometryJsonConverterFactory))]
public readonly partial record struct Ellipse2D
{
    public void Validate()
    {
        ArgGuard.Positive(Radii, "radii");
        ArgGuard.Finite(Center, "center");
        OrientedFrame2D.ValidateAxis(Axis, "axis");
    }

    /// <summary>Stateless validated construction.</summary>
    public static class Builder
    {
        public static Ellipse2D Create(Vector2 radii, Vector2 center = default, float angleRadians = 0f) => new Ellipse2D(radii, center, OrientedFrame2D.AxisFromAngle(angleRadians)).Validated();

        /// <summary>An ellipse whose local +X follows any finite nonzero direction; its length is ignored.</summary>
        public static Ellipse2D Create(Vector2 radii, Vector2 center, Vector2 axis) => new Ellipse2D(radii, center, new Direction2D(axis).Vector).Validated();
    }
}
