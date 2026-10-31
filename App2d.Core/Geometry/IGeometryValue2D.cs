using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using System.Text.Json.Serialization;

namespace App2d.Core.Geometry;

/// <summary>A shape or curve value stored directly in an authored geometry field.</summary>
[JsonConverter(typeof(GeometryJsonConverterFactory))]
public interface IGeometryValue2D
{
    /// <summary>The stable kind ID used by editors and polymorphic JSON.</summary>
    string GetKind() => GetKind(GetType()) ?? GetType().Name;

    /// <summary>Checks this value. Custom implementations can supply their own rules.</summary>
    void Validate() { }

    // Concrete JSON readers know the type before an instance exists; use the same lookup.
    internal static string? GetKind(Type type) => type.Name switch
    {
        nameof(Circle2D) when type == typeof(Circle2D) => ShapeKinds2D.Circle,
        nameof(Ellipse2D) when type == typeof(Ellipse2D) => ShapeKinds2D.Ellipse,
        nameof(Capsule2D) when type == typeof(Capsule2D) => ShapeKinds2D.Capsule,
        nameof(Rectangle2D) when type == typeof(Rectangle2D) => ShapeKinds2D.Rectangle,
        nameof(AxisAlignedRectangle2D) when type == typeof(AxisAlignedRectangle2D) => ShapeKinds2D.AxisAlignedRectangle,
        nameof(RoundedRectangle2D) when type == typeof(RoundedRectangle2D) => ShapeKinds2D.RoundedRectangle,
        nameof(Triangle2D) when type == typeof(Triangle2D) => ShapeKinds2D.Triangle,
        nameof(ConvexPolygon2D) when type == typeof(ConvexPolygon2D) => ShapeKinds2D.ConvexPolygon,
        nameof(SimplePolygon2D) when type == typeof(SimplePolygon2D) => ShapeKinds2D.SimplePolygon,
        nameof(HeightFieldShape2D) when type == typeof(HeightFieldShape2D) => ShapeKinds2D.HeightField,
        nameof(HalfSpace2D) when type == typeof(HalfSpace2D) => ShapeKinds2D.HalfSpace,
        nameof(CompositeShape2D) when type == typeof(CompositeShape2D) => ShapeKinds2D.Composite,
        nameof(LineSegmentCurve2D) when type == typeof(LineSegmentCurve2D) => CurveKinds2D.Line,
        nameof(PolylineCurve2D) when type == typeof(PolylineCurve2D) => CurveKinds2D.Polyline,
        nameof(RoundedPolylineCurve2D) when type == typeof(RoundedPolylineCurve2D) => CurveKinds2D.RoundedPolyline,
        nameof(Arc2D) when type == typeof(Arc2D) => CurveKinds2D.Arc,
        nameof(QuadraticBezier2D) when type == typeof(QuadraticBezier2D) => CurveKinds2D.QuadraticBezier,
        nameof(CubicBezier2D) when type == typeof(CubicBezier2D) => CurveKinds2D.CubicBezier,
        nameof(BSpline2D) when type == typeof(BSpline2D) => CurveKinds2D.BSpline,
        _ => null
    };
}
