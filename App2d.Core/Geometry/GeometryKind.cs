using System.Text.Json.Serialization;

namespace App2d.Core.Geometry;

/// <summary>Identifies a 2D shape or curve.</summary>
[JsonConverter(typeof(GeometryKindJsonConverter))]
public enum GeometryKind
{
    [JsonStringEnumMemberName("custom")]
    Custom,

    [JsonStringEnumMemberName("circle")]
    Circle,

    [JsonStringEnumMemberName("ellipse")]
    Ellipse,

    [JsonStringEnumMemberName("capsule")]
    Capsule,

    [JsonStringEnumMemberName("rectangle")]
    Rectangle,

    [JsonStringEnumMemberName("axis-aligned-rectangle")]
    AxisAlignedRectangle,

    [JsonStringEnumMemberName("rounded-rectangle")]
    RoundedRectangle,

    [JsonStringEnumMemberName("triangle")]
    Triangle,

    [JsonStringEnumMemberName("convex-polygon")]
    ConvexPolygon,

    [JsonStringEnumMemberName("simple-polygon")]
    SimplePolygon,

    [JsonStringEnumMemberName("half-space")]
    HalfSpace,

    [JsonStringEnumMemberName("composite")]
    Composite,

    [JsonStringEnumMemberName("line")]
    LineSegment,

    [JsonStringEnumMemberName("polyline")]
    Polyline,

    [JsonStringEnumMemberName("rounded-polyline")]
    RoundedPolyline,

    [JsonStringEnumMemberName("arc")]
    Arc,

    [JsonStringEnumMemberName("quadratic-bezier")]
    QuadraticBezier,

    [JsonStringEnumMemberName("cubic-bezier")]
    CubicBezier,

    [JsonStringEnumMemberName("b-spline")]
    BSpline,

    [JsonStringEnumMemberName("normal-offset-curve")]
    NormalOffsetCurve,
}

internal sealed class GeometryKindJsonConverter() : JsonStringEnumConverter<GeometryKind>(allowIntegerValues: false);
