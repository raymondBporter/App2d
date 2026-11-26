using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Json;
using System.Text.Json;

namespace App2d.Core.Geometry.Serialization;

/// <summary>JSON and editor defaults for geometry values; no intermediate definition or builder data.</summary>
public static class GeometryJson2D
{
    public static IReadOnlyList<GeometryKind> ShapeKinds { get; } =
    [
        GeometryKind.Circle,
        GeometryKind.Ellipse,
        GeometryKind.Capsule,
        GeometryKind.Rectangle,
        GeometryKind.AxisAlignedRectangle,
        GeometryKind.RoundedRectangle,
        GeometryKind.Triangle,
        GeometryKind.ConvexPolygon,
        GeometryKind.SimplePolygon,
        GeometryKind.HalfSpace,
        GeometryKind.Composite
    ];

    public static IReadOnlyList<GeometryKind> CurveKinds { get; } =
    [
        GeometryKind.LineSegment,
        GeometryKind.Polyline,
        GeometryKind.RoundedPolyline,
        GeometryKind.Arc,
        GeometryKind.QuadraticBezier,
        GeometryKind.CubicBezier,
        GeometryKind.BSpline
    ];

    /// <summary>Geometry kinds with a drawable curve or closed outline.</summary>
    public static IReadOnlyList<GeometryKind> DrawableKinds { get; } =
        [.. ShapeKinds.Where(kind => kind is not (GeometryKind.HalfSpace or GeometryKind.Composite)), .. CurveKinds];

    internal static GeometryKind ReadKind(JsonElement tag)
    {
        // Geometry tags are single exact names. The enum converter also accepts whitespace and comma-separated names.
        if (tag.ValueKind != JsonValueKind.String || tag.GetString() is not { } name ||
            name.AsSpan().Trim().Length != name.Length || name.Contains(','))
        {
            throw new JsonException("Expected a geometry kind string.");
        }

        return tag.Deserialize<GeometryKind>();
    }

    /// <summary>A small editable example of a geometry kind. Drawing clients supply placement and material.</summary>
    public static IGeometryValue2D CreateDefault(GeometryKind kind) => kind switch
    {
        GeometryKind.Capsule => new Capsule2 { Start = new(0, -.25f), End = new(0, .25f), Radius = .25f }.Validated(),
        GeometryKind.RoundedRectangle => RoundedRectangle2D.Builder.FromSize(.4f, .4f, .05f),
        GeometryKind.LineSegment => new LineSegmentCurve2D { Start = Vector2f.Zero, End = Vector2f.UnitY }.Validated(),
        GeometryKind.Polyline => new PolylineCurve2D { Points = [Vector2f.Zero, new(.25f, .5f), Vector2f.UnitY] }.Validated(),
        GeometryKind.RoundedPolyline => new RoundedPolylineCurve2D { Points = [Vector2f.Zero, new(.25f, .5f), Vector2f.UnitY] }.Validated(),
        GeometryKind.Arc => new Arc2D { Center = new(0, .5f), Radius = .5f, StartAngleRadians = -MathF.PI / 2, SweepAngleRadians = MathF.PI }.Validated(),
        GeometryKind.QuadraticBezier => new QuadraticBezier2D { Start = Vector2f.Zero, Control = new(.25f, .5f), End = Vector2f.UnitY }.Validated(),
        GeometryKind.CubicBezier => new CubicBezier2D { Start = Vector2f.Zero, Control1 = new(.25f, 1f / 3), Control2 = new(-.25f, 2f / 3), End = Vector2f.UnitY }.Validated(),
        GeometryKind.BSpline => new BSpline2D { ControlPoints = [Vector2f.Zero, new(.25f, 1f / 3), new(-.25f, 2f / 3), Vector2f.UnitY] }.Validated(),
        _ => new Ellipse2D { Center = Vector2f.Zero, Radii = new(.5f, .5f) }.Validated().WithKind(kind)
    };

    public static JsonSerializerOptions JsonOptions => JsonProfiles.Geometry;

    /// <summary>Writes a kind tag only when the declared geometry type is an interface.</summary>
    public static string ToJson<T>(this T geometry) where T : IGeometryValue2D
    {
        ArgumentNullException.ThrowIfNull(geometry);
        return JsonSerializer.Serialize(geometry, JsonOptions);
    }
    public static IGeometryValue2D FromJson(string json) => JsonSerializer.Deserialize<IGeometryValue2D>(json, JsonOptions) ?? throw new JsonException("Empty geometry.");
    public static IShape2D ShapeFromJson(string json) => JsonSerializer.Deserialize<IShape2D>(json, JsonOptions) ?? throw new JsonException("Empty shape.");
    public static ICurve2D CurveFromJson(string json) => JsonSerializer.Deserialize<ICurve2D>(json, JsonOptions) ?? throw new JsonException("Empty curve.");
}
