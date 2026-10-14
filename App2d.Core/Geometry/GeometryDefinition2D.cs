using App2d.Core.Curves;
using App2d.Core.IO;
using App2d.Core.Shapes;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Geometry;

/// <summary>
/// Tagged, editable data for a built-in shape or curve. Runtime geometry remains independent of JSON.
/// The existing shape and curve definitions retain their JSON representation when read through this root.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(CircleShapeDefinition2D), ShapeKinds2D.Circle)]
[JsonDerivedType(typeof(EllipseShapeDefinition2D), ShapeKinds2D.Ellipse)]
[JsonDerivedType(typeof(CapsuleShapeDefinition2D), ShapeKinds2D.Capsule)]
[JsonDerivedType(typeof(RectangleShapeDefinition2D), ShapeKinds2D.Rectangle)]
[JsonDerivedType(typeof(RoundedRectangleShapeDefinition2D), ShapeKinds2D.RoundedRectangle)]
[JsonDerivedType(typeof(TriangleShapeDefinition2D), ShapeKinds2D.Triangle)]
[JsonDerivedType(typeof(ConvexPolygonShapeDefinition2D), ShapeKinds2D.ConvexPolygon)]
[JsonDerivedType(typeof(SimplePolygonShapeDefinition2D), ShapeKinds2D.SimplePolygon)]
[JsonDerivedType(typeof(HalfSpaceShapeDefinition2D), ShapeKinds2D.HalfSpace)]
[JsonDerivedType(typeof(CompositeShapeDefinition2D), ShapeKinds2D.Composite)]
[JsonDerivedType(typeof(LineCurveDefinition2D), CurveKinds2D.Line)]
[JsonDerivedType(typeof(PolylineCurveDefinition2D), CurveKinds2D.Polyline)]
[JsonDerivedType(typeof(ArcCurveDefinition2D), CurveKinds2D.Arc)]
[JsonDerivedType(typeof(QuadraticBezierCurveDefinition2D), CurveKinds2D.QuadraticBezier)]
[JsonDerivedType(typeof(CubicBezierCurveDefinition2D), CurveKinds2D.CubicBezier)]
[JsonDerivedType(typeof(BSplineCurveDefinition2D), CurveKinds2D.BSpline)]
public abstract record GeometryDefinition2D
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowOutOfOrderMetadataProperties = true,
        Converters = { new Vector2JsonConverter() }
    };

    public string ToGeometryJson() => JsonSerializer.Serialize<GeometryDefinition2D>(this, JsonOptions);

    public static GeometryDefinition2D FromJson(string json) =>
        JsonSerializer.Deserialize<GeometryDefinition2D>(json, JsonOptions)
        ?? throw new JsonException("Empty geometry definition.");

    public static GeometryDefinition2D FromShape(IShape2D shape) => ShapeDefinition2D.FromShape(shape);

    public static GeometryDefinition2D FromCurve(ICurve2D curve) => CurveDefinition2D.FromCurve(curve);
}
