using App2d.Core.Json;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Operations.Conversion;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Geometry.Serialization;

/// <summary>JSON and editor defaults for geometry values; no intermediate definition or builder data.</summary>
public static class GeometryJson2D
{
    /// <summary>Geometry kinds with a drawable curve or closed outline.</summary>
    public static IReadOnlyList<string> DrawableKinds { get; } = [.. ShapeKinds2D.All.Where(kind => kind is not ShapeKinds2D.HalfSpace and not ShapeKinds2D.Composite), .. CurveKinds2D.All];

    /// <summary>A small editable example of a geometry kind. Drawing clients supply placement and material.</summary>
    public static IGeometryValue2D CreateDefault(string kind) => kind switch
    {
        ShapeKinds2D.Capsule => new Capsule2D { Start = new(0, -.25f), End = new(0, .25f), Radius = .25f }.Validated(),
        ShapeKinds2D.RoundedRectangle => RoundedRectangle2D.Builder.FromSize(.4f, .4f, .05f),
        CurveKinds2D.Line => new LineSegmentCurve2D { Start = Vector2.Zero, End = Vector2.UnitY }.Validated(),
        CurveKinds2D.Polyline => new PolylineCurve2D { Points = [Vector2.Zero, new(.25f, .5f), Vector2.UnitY] }.Validated(),
        CurveKinds2D.RoundedPolyline => new RoundedPolylineCurve2D { Points = [Vector2.Zero, new(.25f, .5f), Vector2.UnitY] }.Validated(),
        CurveKinds2D.Arc => new Arc2D { Center = new(0, .5f), Radius = .5f, StartAngleRadians = -MathF.PI / 2, SweepAngleRadians = MathF.PI }.Validated(),
        CurveKinds2D.QuadraticBezier => new QuadraticBezier2D { Start = Vector2.Zero, Control = new(.25f, .5f), End = Vector2.UnitY }.Validated(),
        CurveKinds2D.CubicBezier => new CubicBezier2D { Start = Vector2.Zero, Control1 = new(.25f, 1f / 3), Control2 = new(-.25f, 2f / 3), End = Vector2.UnitY }.Validated(),
        CurveKinds2D.BSpline => new BSpline2D { ControlPoints = [Vector2.Zero, new(.25f, 1f / 3), new(-.25f, 2f / 3), Vector2.UnitY] }.Validated(),
        _ => new Ellipse2D { Center = Vector2.Zero, Radii = new(.5f, .5f) }.Validated().WithKind(kind)
    };

    public static JsonSerializerOptions JsonOptions => JsonProfiles.Geometry;

    public static string ToJson(this IGeometryValue2D geometry)
    {
        ArgumentNullException.ThrowIfNull(geometry);
        return JsonSerializer.Serialize(geometry, JsonOptions);
    }
    public static IGeometryValue2D FromJson(string json) => JsonSerializer.Deserialize<IGeometryValue2D>(json, JsonOptions) ?? throw new JsonException("Empty geometry.");
    public static IShape2D ShapeFromJson(string json) => JsonSerializer.Deserialize<IShape2D>(json, JsonOptions) ?? throw new JsonException("Empty shape.");
    public static ICurve2D CurveFromJson(string json) => JsonSerializer.Deserialize<ICurve2D>(json, JsonOptions) ?? throw new JsonException("Empty curve.");
}
