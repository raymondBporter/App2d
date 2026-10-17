using App2d.Core.Characters;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Serialization;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Curves;

public sealed class CurveSerialization2DTests
{
    public static TheoryData<string> BuiltInCurves =>
    [
        CurveKinds2D.Line,
        CurveKinds2D.Polyline,
        CurveKinds2D.Arc,
        CurveKinds2D.QuadraticBezier,
        CurveKinds2D.CubicBezier,
        CurveKinds2D.BSpline
    ];

    private static ICurve2D CreateCurve(string kind) => kind switch
    {
        CurveKinds2D.Line => LineSegmentCurve2D.Builder.Create(new(-2, 1), new(3, 4)),
        CurveKinds2D.Polyline => PolylineCurve2D.Builder.Create([new(-2, 1), new(1, 3), new(3, 4)]),
        CurveKinds2D.Arc => Arc2D.Builder.Create(new(1, -2), 3, .25f, -2.5f),
        CurveKinds2D.QuadraticBezier => QuadraticBezier2D.Builder.Create(new(0, 0), new(2, 3), new(4, 0)),
        CurveKinds2D.CubicBezier => CubicBezier2D.Builder.Create(new(0, 0), new(1, 3), new(3, -2), new(5, 1)),
        CurveKinds2D.BSpline => BSpline2D.Builder.Create([new(0, 0), new(1, 2), new(2, -1), new(3, 2), new(4, 0)]),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    [Theory]
    [MemberData(nameof(BuiltInCurves))]
    public void TaggedValuesRoundTripIntoEquivalentRuntimeCurves(string kind)
    {
        var original = CreateCurve(kind);
        var json = original.ToJson();
        using var document = JsonDocument.Parse(json);
        Assert.Equal(kind, document.RootElement.GetProperty("kind").GetString());
        var restored = GeometryJson2D.CurveFromJson(json).Validated();
        Assert.Equal(original.GetType(), restored.GetType());
        foreach (var amount in new[] { 0f, .13f, .5f, .87f, 1f })
        {
            Assert.InRange(Vector2.Distance(original.Evaluate(amount), restored.Evaluate(amount)), 0, 1e-5f);
            Assert.InRange(Vector2.Distance(original.EvaluateDerivative(amount), restored.EvaluateDerivative(amount)), 0, 1e-5f);
        }
    }

    [Fact]
    public void ShapeAndDegreeDataSurviveTheRoundTrip()
    {
        var original = BSpline2D.Builder.Create([new(0, 0), new(1, 2), new(3, 0)], degree: 2);
        var definition = Assert.IsType<BSpline2D>(GeometryJson2D.CurveFromJson(original.ToJson()));
        Assert.Equal(2, definition.Degree);
        Assert.Equal(original.ControlPoints.ToArray(), definition.ControlPoints.ToArray());
    }

    private sealed class CurveEnvelope
    {
        public required ICurve2D Path
        {
            get; init;
        }
    }

    [Fact]
    public void GeometryWorksInsideExistingAuthoredJsonOptions()
    {
        var envelope = new CurveEnvelope
        {
            Path = new LineSegmentCurve2D { Start = new(1, 2), End = new(3, 4) }
        };
        var json = JsonSerializer.Serialize(envelope, AuthoredJson.Options);
        Assert.Contains("\"kind\": \"line\"", json);
        Assert.Contains("\"x\": 1", json);
        var restored = JsonSerializer.Deserialize<CurveEnvelope>(json, AuthoredJson.Options)!;
        Assert.Equal(new Vector2(3, 4), restored.Path.Validated().Evaluate(1));
    }

    [Fact]
    public void UnknownKindsMissingFieldsAndMisspelledFieldsAreRejected()
    {
        Assert.Throws<JsonException>(() => GeometryJson2D.CurveFromJson("{\"kind\":\"spiral\"}"));
        Assert.Throws<JsonException>(() => GeometryJson2D.CurveFromJson("{\"kind\":\"line\",\"start\":{\"x\":0,\"y\":0}}"));
        Assert.Throws<JsonException>(() => GeometryJson2D.CurveFromJson("{\"kind\":\"line\",\"start\":{\"x\":0,\"y\":0},\"end\":{\"x\":1,\"y\":1},\"edn\":0}"));
    }

    [Fact]
    public void InvalidCurveParametersFailWhenValidated()
    {
        var definition = GeometryJson2D.CurveFromJson("{\"kind\":\"b-spline\",\"degree\":3,\"controlPoints\":[{\"x\":0,\"y\":0},{\"x\":1,\"y\":1}]}");
        Assert.Throws<ArgumentException>(definition.Validate);
        Assert.Throws<ArgumentException>(definition.Validate);
    }

    public static TheoryData<ICurve2D> InvalidValues =>
    [
        new LineSegmentCurve2D { Start = new(float.NaN, 0), End = Vector2.One },
        new LineSegmentCurve2D { Start = Vector2.Zero, End = new(0, float.PositiveInfinity) },
        new PolylineCurve2D { Points = null! },
        new PolylineCurve2D { Points = [Vector2.Zero] },
        new PolylineCurve2D { Points = [Vector2.Zero, new(float.NaN, 1)] },
        new Arc2D { Center = Vector2.Zero, Radius = -1, StartAngleRadians = 0, SweepAngleRadians = 1 },
        new Arc2D { Center = new(float.NaN, 0), Radius = 1, StartAngleRadians = 0, SweepAngleRadians = 1 },
        new Arc2D { Center = Vector2.Zero, Radius = 1, StartAngleRadians = float.NaN, SweepAngleRadians = 1 },
        new Arc2D { Center = Vector2.Zero, Radius = 1, StartAngleRadians = 0, SweepAngleRadians = float.PositiveInfinity },
        new QuadraticBezier2D { Start = Vector2.Zero, Control = new(float.NaN, 0), End = Vector2.One },
        new CubicBezier2D { Start = Vector2.Zero, Control1 = Vector2.UnitY, Control2 = new(float.NaN, 0), End = Vector2.One },
        new BSpline2D { ControlPoints = null! },
        new BSpline2D { ControlPoints = [Vector2.Zero, Vector2.One], Degree = 0 },
        new BSpline2D { ControlPoints = [Vector2.Zero, Vector2.One], Degree = 2 },
        new BSpline2D { ControlPoints = [Vector2.Zero, new(float.NaN, 0)], Degree = 1 }
    ];

    [Theory]
    [MemberData(nameof(InvalidValues))]
    public void ValidationRejectsInvalidEditableGeometry(ICurve2D builder)
    {
        Assert.ThrowsAny<ArgumentException>(builder.Validate);
        Assert.ThrowsAny<ArgumentException>(() => builder.Validated());
    }

    [Theory]
    [InlineData("{\"kind\":\"arc\",\"center\":{\"x\":0,\"y\":0},\"radius\":-1,\"startAngleRadians\":0,\"sweepAngleRadians\":1}")]
    [InlineData("{\"kind\":\"polyline\",\"points\":[{\"x\":0,\"y\":0}]}")]
    [InlineData("{\"kind\":\"b-spline\",\"degree\":0,\"controlPoints\":[{\"x\":0,\"y\":0},{\"x\":1,\"y\":1}]}")]
    public void DeserializationDefersGeometryValidationForBothBuilderRoots(string json)
    {
        var curve = GeometryJson2D.CurveFromJson(json);
        var geometry = GeometryJson2D.FromJson(json);
        Assert.Equal(curve.GetType(), geometry.GetType());
        Assert.ThrowsAny<ArgumentException>(curve.Validate);
        Assert.ThrowsAny<ArgumentException>(geometry.Validate);
    }

    [Fact]
    public void BuiltCurvesAreValueTypesAndKeepSnapshotsOfEditablePoints()
    {
        foreach (var kind in BuiltInCurves)
        {
            var type = CreateCurve(kind).GetType();
            Assert.True(type.IsValueType);
            Assert.True(type.IsDefined(typeof(System.Runtime.CompilerServices.IsReadOnlyAttribute), inherit: false));
        }

        Vector2[] points = [Vector2.Zero, Vector2.One];
        PolylineCurve2D polyline = PolylineCurve2D.Builder.Create(points);
        BSpline2D spline = BSpline2D.Builder.Create(points, 1);
        points[1] = new(100, 100);
        Assert.Equal(Vector2.One, polyline.Evaluate(1));
        Assert.Equal(Vector2.One, spline.Evaluate(1));
        var quadratic = BSpline2D.Builder.Create([Vector2.Zero, new(1, 2), new(2, 0)], 2);
        Assert.Equal(new Vector2(1, 1), quadratic.Evaluate(.5f));
        Assert.Equal(new Vector2(1, 2), (quadratic with { Degree = 1 }).Evaluate(.5f));
        Assert.Equal(new Vector2(1, 1), quadratic.Evaluate(.5f));
        var edited = spline with { ControlPoints = [Vector2.Zero, new(3, 3)] };
        Assert.Equal(new Vector2(3, 3), edited.Evaluate(1));
        Assert.Equal(Vector2.One, spline.Evaluate(1));
    }

    [Fact]
    public void CurvesWithExecutableProfilesHaveNoJsonSchema()
    {
        var curve = new NormalOffsetCurve2D(LineSegmentCurve2D.Builder.Create(Vector2.Zero, Vector2.One), _ => 1);
        Assert.Throws<NotSupportedException>(() => curve.ToJson());
    }
}
