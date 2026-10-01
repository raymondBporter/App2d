using App2d.Core.Geometry;
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
[JsonDerivedType(typeof(ArcCurveDefinition2D), CurveKinds2D.Arc)]
[JsonDerivedType(typeof(QuadraticBezierCurveDefinition2D), CurveKinds2D.QuadraticBezier)]
[JsonDerivedType(typeof(CubicBezierCurveDefinition2D), CurveKinds2D.CubicBezier)]
[JsonDerivedType(typeof(BSplineCurveDefinition2D), CurveKinds2D.BSpline)]
public abstract class CurveDefinition2D
{
    public static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowOutOfOrderMetadataProperties = true
    };

    public abstract ICurve2D Build();

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static CurveDefinition2D FromJson(string json) => JsonSerializer.Deserialize<CurveDefinition2D>(json, JsonOptions)
        ?? throw new JsonException("Empty curve definition.");

    /// <summary>Copies a built-in runtime curve into editable data. Arbitrary implementations have no known schema.</summary>
    public static CurveDefinition2D FromCurve(ICurve2D curve) => curve switch
    {
        LineSegmentCurve2D line => new LineCurveDefinition2D
        {
            Start = Point2D.From(line.Start),
            End = Point2D.From(line.End)
        },
        Arc2D arc => new ArcCurveDefinition2D
        {
            Center = Point2D.From(arc.Center),
            Radius = arc.Radius,
            StartAngleRadians = arc.StartAngleRadians,
            SweepAngleRadians = arc.SweepAngleRadians
        },
        QuadraticBezier2D quadratic => new QuadraticBezierCurveDefinition2D
        {
            Start = Point2D.From(quadratic.Start),
            Control = Point2D.From(quadratic.Control),
            End = Point2D.From(quadratic.End)
        },
        CubicBezier2D cubic => new CubicBezierCurveDefinition2D
        {
            Start = Point2D.From(cubic.Start),
            Control1 = Point2D.From(cubic.Control1),
            Control2 = Point2D.From(cubic.Control2),
            End = Point2D.From(cubic.End)
        },
        BSpline2D spline => new BSplineCurveDefinition2D
        {
            Degree = spline.Degree,
            ControlPoints = [.. spline.ControlPoints.Select(Point2D.From)]
        },
        null => throw new ArgumentNullException(nameof(curve)),
        _ => throw new NotSupportedException($"No curve definition exists for {curve.GetType().Name}.")
    };
}

public sealed class LineCurveDefinition2D : CurveDefinition2D
{
    public required Point2D Start { get; init; }
    public required Point2D End { get; init; }
    public override ICurve2D Build() => new LineSegmentCurve2D(Start.Vector, End.Vector);
}

public sealed class ArcCurveDefinition2D : CurveDefinition2D
{
    public required Point2D Center { get; init; }
    public required float Radius { get; init; }
    public required float StartAngleRadians { get; init; }
    public required float SweepAngleRadians { get; init; }
    public override ICurve2D Build() => new Arc2D(Center.Vector, Radius, StartAngleRadians, SweepAngleRadians);
}

public sealed class QuadraticBezierCurveDefinition2D : CurveDefinition2D
{
    public required Point2D Start { get; init; }
    public required Point2D Control { get; init; }
    public required Point2D End { get; init; }
    public override ICurve2D Build() => new QuadraticBezier2D(Start.Vector, Control.Vector, End.Vector);
}

public sealed class CubicBezierCurveDefinition2D : CurveDefinition2D
{
    public required Point2D Start { get; init; }
    public required Point2D Control1 { get; init; }
    public required Point2D Control2 { get; init; }
    public required Point2D End { get; init; }
    public override ICurve2D Build() => new CubicBezier2D(Start.Vector, Control1.Vector, Control2.Vector, End.Vector);
}

public sealed class BSplineCurveDefinition2D : CurveDefinition2D
{
    public required List<Point2D> ControlPoints { get; init; }
    public int Degree { get; init; } = 3;
    public override ICurve2D Build() => new BSpline2D(ControlPoints.Select(point => point.Vector), Degree);
}
