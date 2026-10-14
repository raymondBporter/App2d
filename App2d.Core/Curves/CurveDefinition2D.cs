using App2d.Core.Geometry;
using App2d.Core.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Core.Curves;

/// <summary>
/// Editable, tagged data for the built-in two-dimensional curves. Runtime curves stay independent of JSON;
/// custom curves such as NormalOffsetCurve2D need their own definition before they can be saved.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(LineCurveDefinition2D), CurveKinds2D.Line)]
[JsonDerivedType(typeof(PolylineCurveDefinition2D), CurveKinds2D.Polyline)]
[JsonDerivedType(typeof(ArcCurveDefinition2D), CurveKinds2D.Arc)]
[JsonDerivedType(typeof(QuadraticBezierCurveDefinition2D), CurveKinds2D.QuadraticBezier)]
[JsonDerivedType(typeof(CubicBezierCurveDefinition2D), CurveKinds2D.CubicBezier)]
[JsonDerivedType(typeof(BSplineCurveDefinition2D), CurveKinds2D.BSpline)]
public abstract record CurveDefinition2D : GeometryDefinition2D
{
    public new static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowOutOfOrderMetadataProperties = true,
        Converters = { new Vector2JsonConverter() }
    };

    public abstract ICurve2D Build();

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public new static CurveDefinition2D FromJson(string json) => JsonSerializer.Deserialize<CurveDefinition2D>(json, JsonOptions)
        ?? throw new JsonException("Empty curve definition.");

    /// <summary>Copies a built-in runtime curve into editable data. Arbitrary implementations have no known schema.</summary>
    public new static CurveDefinition2D FromCurve(ICurve2D curve) => curve switch
    {
        LineSegmentCurve2D line => new LineCurveDefinition2D
        {
            Start = line.Start,
            End = line.End
        },
        PolylineCurve2D polyline => new PolylineCurveDefinition2D
        {
            Points = [.. polyline.Points]
        },
        Arc2D arc => new ArcCurveDefinition2D
        {
            Center = arc.Center,
            Radius = arc.Radius,
            StartAngleRadians = arc.StartAngleRadians,
            SweepAngleRadians = arc.SweepAngleRadians
        },
        QuadraticBezier2D quadratic => new QuadraticBezierCurveDefinition2D
        {
            Start = quadratic.Start,
            Control = quadratic.Control,
            End = quadratic.End
        },
        CubicBezier2D cubic => new CubicBezierCurveDefinition2D
        {
            Start = cubic.Start,
            Control1 = cubic.Control1,
            Control2 = cubic.Control2,
            End = cubic.End
        },
        BSpline2D spline => new BSplineCurveDefinition2D
        {
            Degree = spline.Degree,
            ControlPoints = [.. spline.ControlPoints]
        },
        null => throw new ArgumentNullException(nameof(curve)),
        _ => throw new NotSupportedException($"No curve definition exists for {curve.GetType().Name}.")
    };
}

public sealed record LineCurveDefinition2D : CurveDefinition2D
{
    public required Vector2 Start { get; init; }
    public required Vector2 End { get; init; }
    public override ICurve2D Build() => new LineSegmentCurve2D(Start, End);
}

public sealed record PolylineCurveDefinition2D : CurveDefinition2D
{
    public required List<Vector2> Points { get; init; }
    public override ICurve2D Build() => new PolylineCurve2D(Points);
}

public sealed record ArcCurveDefinition2D : CurveDefinition2D
{
    public required Vector2 Center { get; init; }
    public required float Radius { get; init; }
    public required float StartAngleRadians { get; init; }
    public required float SweepAngleRadians { get; init; }
    public override ICurve2D Build() => new Arc2D(Center, Radius, StartAngleRadians, SweepAngleRadians);
}

public sealed record QuadraticBezierCurveDefinition2D : CurveDefinition2D
{
    public required Vector2 Start { get; init; }
    public required Vector2 Control { get; init; }
    public required Vector2 End { get; init; }
    public override ICurve2D Build() => new QuadraticBezier2D(Start, Control, End);
}

public sealed record CubicBezierCurveDefinition2D : CurveDefinition2D
{
    public required Vector2 Start { get; init; }
    public required Vector2 Control1 { get; init; }
    public required Vector2 Control2 { get; init; }
    public required Vector2 End { get; init; }
    public override ICurve2D Build() => new CubicBezier2D(Start, Control1, Control2, End);
}

public sealed record BSplineCurveDefinition2D : CurveDefinition2D
{
    public required List<Vector2> ControlPoints { get; init; }
    public int Degree { get; init; } = 3;
    public override ICurve2D Build() => new BSpline2D(ControlPoints, Degree);
}
