using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Json;
using System.Text.Json;

namespace App2d.Core.Geometry.Serialization;

/// <summary>JSON and editor defaults for geometry values; no intermediate definition or builder data.</summary>
public static class GeometryJson2D
{
    private static readonly Dictionary<Type, string> KindByType = [];

    public static IReadOnlyList<string> ShapeKinds { get; } =
    [
        Register<Circle2D>(Circle2D.Kind),
        Register<Ellipse2D>(Ellipse2D.Kind),
        Register<Capsule2>(Capsule2.Kind),
        Register<Rectangle2D>(Rectangle2D.Kind),
        Register<AxisAlignedRectangle2D>(AxisAlignedRectangle2D.Kind),
        Register<RoundedRectangle2D>(RoundedRectangle2D.Kind),
        Register<Triangle2D>(Triangle2D.Kind),
        Register<ConvexPolygon2D>(ConvexPolygon2D.Kind),
        Register<SimplePolygon2D>(SimplePolygon2D.Kind),
        Register<HalfSpace2D>(HalfSpace2D.Kind),
        Register<CompositeShape2D>(CompositeShape2D.Kind)
    ];

    public static IReadOnlyList<string> CurveKinds { get; } =
    [
        Register<LineSegmentCurve2D>(LineSegmentCurve2D.Kind),
        Register<PolylineCurve2D>(PolylineCurve2D.Kind),
        Register<RoundedPolylineCurve2D>(RoundedPolylineCurve2D.Kind),
        Register<Arc2D>(Arc2D.Kind),
        Register<QuadraticBezier2D>(QuadraticBezier2D.Kind),
        Register<CubicBezier2D>(CubicBezier2D.Kind),
        Register<BSpline2D>(BSpline2D.Kind)
    ];

    /// <summary>Geometry kinds with a drawable curve or closed outline.</summary>
    public static IReadOnlyList<string> DrawableKinds { get; } = [.. ShapeKinds.Where(kind => kind is not HalfSpace2D.Kind and not CompositeShape2D.Kind), .. CurveKinds];

    // The same registration supplies the ordered catalog and the tag for untagged concrete JSON.
    private static string Register<T>(string kind) where T : IGeometryValue2D
    {
        KindByType.Add(typeof(T), kind);
        return kind;
    }

    internal static string? GetKind(Type type) => KindByType.GetValueOrDefault(type);

    /// <summary>A small editable example of a geometry kind. Drawing clients supply placement and material.</summary>
    public static IGeometryValue2D CreateDefault(string kind) => kind switch
    {
        Capsule2.Kind => new Capsule2 { Start = new(0, -.25f), End = new(0, .25f), Radius = .25f }.Validated(),
        RoundedRectangle2D.Kind => RoundedRectangle2D.Builder.FromSize(.4f, .4f, .05f),
        LineSegmentCurve2D.Kind => new LineSegmentCurve2D { Start = Vector2f.Zero, End = Vector2f.UnitY }.Validated(),
        PolylineCurve2D.Kind => new PolylineCurve2D { Points = [Vector2f.Zero, new(.25f, .5f), Vector2f.UnitY] }.Validated(),
        RoundedPolylineCurve2D.Kind => new RoundedPolylineCurve2D { Points = [Vector2f.Zero, new(.25f, .5f), Vector2f.UnitY] }.Validated(),
        Arc2D.Kind => new Arc2D { Center = new(0, .5f), Radius = .5f, StartAngleRadians = -MathF.PI / 2, SweepAngleRadians = MathF.PI }.Validated(),
        QuadraticBezier2D.Kind => new QuadraticBezier2D { Start = Vector2f.Zero, Control = new(.25f, .5f), End = Vector2f.UnitY }.Validated(),
        CubicBezier2D.Kind => new CubicBezier2D { Start = Vector2f.Zero, Control1 = new(.25f, 1f / 3), Control2 = new(-.25f, 2f / 3), End = Vector2f.UnitY }.Validated(),
        BSpline2D.Kind => new BSpline2D { ControlPoints = [Vector2f.Zero, new(.25f, 1f / 3), new(-.25f, 2f / 3), Vector2f.UnitY] }.Validated(),
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
