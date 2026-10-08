using App2d.Core.Characters;
using App2d.Core.Curves;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Curves;

public sealed class CurveDefinition2DTests
{
    public static TheoryData<ICurve2D, string> BuiltInCurves => new()
    {
        { new LineSegmentCurve2D(new(-2, 1), new(3, 4)), CurveKinds2D.Line },
        { new Arc2D(new(1, -2), 3, .25f, -2.5f), CurveKinds2D.Arc },
        { new QuadraticBezier2D(new(0, 0), new(2, 3), new(4, 0)), CurveKinds2D.QuadraticBezier },
        { new CubicBezier2D(new(0, 0), new(1, 3), new(3, -2), new(5, 1)), CurveKinds2D.CubicBezier },
        { new BSpline2D([new(0, 0), new(1, 2), new(2, -1), new(3, 2), new(4, 0)]), CurveKinds2D.BSpline }
    };

    [Theory]
    [MemberData(nameof(BuiltInCurves))]
    public void TaggedDefinitionsRoundTripIntoEquivalentRuntimeCurves(ICurve2D original, string kind)
    {
        var json = CurveDefinition2D.FromCurve(original).ToJson();
        using var document = JsonDocument.Parse(json);
        Assert.Equal(kind, document.RootElement.GetProperty("kind").GetString());
        var restored = CurveDefinition2D.FromJson(json).Build();
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
        var original = new BSpline2D([new(0, 0), new(1, 2), new(3, 0)], degree: 2);
        var definition = Assert.IsType<BSplineCurveDefinition2D>(CurveDefinition2D.FromJson(CurveDefinition2D.FromCurve(original).ToJson()));
        Assert.Equal(2, definition.Degree);
        Assert.Equal(original.ControlPoints, definition.ControlPoints);
    }

    private sealed class CurveEnvelope
    {
        public required CurveDefinition2D Path { get; init; }
    }

    [Fact]
    public void DefinitionWorksInsideExistingAuthoredJsonOptions()
    {
        var envelope = new CurveEnvelope
        {
            Path = new LineCurveDefinition2D { Start = new(1, 2), End = new(3, 4) }
        };
        var json = JsonSerializer.Serialize(envelope, AuthoredJson.Options);
        Assert.Contains("\"kind\": \"line\"", json);
        Assert.Contains("\"x\": 1", json);
        var restored = JsonSerializer.Deserialize<CurveEnvelope>(json, AuthoredJson.Options)!;
        Assert.Equal(new Vector2(3, 4), restored.Path.Build().Evaluate(1));
    }

    [Fact]
    public void UnknownKindsMissingFieldsAndMisspelledFieldsAreRejected()
    {
        Assert.Throws<JsonException>(() => CurveDefinition2D.FromJson("{\"kind\":\"spiral\"}"));
        Assert.Throws<JsonException>(() => CurveDefinition2D.FromJson("{\"kind\":\"line\",\"start\":{\"x\":0,\"y\":0}}"));
        Assert.Throws<JsonException>(() => CurveDefinition2D.FromJson("{\"kind\":\"line\",\"start\":{\"x\":0,\"y\":0},\"end\":{\"x\":1,\"y\":1},\"edn\":0}"));
    }

    [Fact]
    public void InvalidCurveParametersFailWhenBuilt()
    {
        var definition = CurveDefinition2D.FromJson("{\"kind\":\"b-spline\",\"degree\":3,\"controlPoints\":[{\"x\":0,\"y\":0},{\"x\":1,\"y\":1}]}");
        Assert.Throws<ArgumentException>(definition.Build);
    }

    [Fact]
    public void CurvesWithExecutableProfilesNeedAnExplicitDefinition()
    {
        var curve = new NormalOffsetCurve2D(new LineSegmentCurve2D(Vector2.Zero, Vector2.One), _ => 1);
        Assert.Throws<NotSupportedException>(() => CurveDefinition2D.FromCurve(curve));
    }
}
