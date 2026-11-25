using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Json;
using System.Text.Json;

namespace App2d.Tests.Geometry;

public sealed class ConcreteGeometryJsonTests
{
    public static TheoryData<GeometryKind> Kinds => [.. GeometryJson2D.ShapeKinds, .. GeometryJson2D.CurveKinds];

    [Fact]
    public void BuiltInCatalogsKeepTheirWireNamesAndOrder()
    {
        Assert.Equal([
            "circle", "ellipse", "capsule", "rectangle", "axis-aligned-rectangle", "rounded-rectangle",
            "triangle", "convex-polygon", "simple-polygon", "half-space", "composite"
        ], JsonSerializer.SerializeToElement(GeometryJson2D.ShapeKinds).EnumerateArray().Select(tag => tag.GetString()));
        Assert.Equal([
            "line", "polyline", "rounded-polyline", "arc", "quadratic-bezier", "cubic-bezier", "b-spline"
        ], JsonSerializer.SerializeToElement(GeometryJson2D.CurveKinds).EnumerateArray().Select(tag => tag.GetString()));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void ConcreteTypesRoundTripWithoutAKindTag(GeometryKind kind)
    {
        var original = GeometryJson2D.CreateDefault(kind);
        Assert.Equal(kind, original.Kind);
        var type = original.GetType();
        var json = JsonSerializer.Serialize(original, type, GeometryJson2D.JsonOptions);
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("kind", out _));
        var restored = Assert.IsType<IGeometryValue2D>(JsonSerializer.Deserialize(json, type, GeometryJson2D.JsonOptions), exactMatch: false);
        restored.Validate();
        Assert.Equal(type, restored.GetType());
        Assert.Equal(original.ToJson(), restored.ToJson());
        Assert.Throws<JsonException>(() => GeometryJson2D.FromJson(json));
    }

    [Fact]
    public void ToJsonUsesTheDeclaredType()
    {
        var circle = Circle2D.Builder.Create(3, new(1, 2));
        using var concrete = JsonDocument.Parse(circle.ToJson());
        using var polymorphic = JsonDocument.Parse(circle.ToJson<IGeometryValue2D>());
        Assert.False(concrete.RootElement.TryGetProperty("kind", out _));
        Assert.Equal("circle", polymorphic.RootElement.GetProperty("kind").GetString());
        Assert.Equal(circle, JsonSerializer.Deserialize<Circle2D>(circle.ToJson(), GeometryJson2D.JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Rectangle2D>(polymorphic.RootElement.GetRawText(), GeometryJson2D.JsonOptions));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"1\"")]
    [InlineData("\"Circle\"")]
    [InlineData("\" circle \"")]
    [InlineData("\"circle,circle\"")]
    public void KindTagsRequireASingleExactWireName(string tag)
    {
        var json = $$"""{"kind":{{tag}},"center":{"x":0,"y":0},"radius":1}""";
        Assert.Throws<JsonException>(() => GeometryJson2D.FromJson(json));
    }

    private sealed record Fields(Circle2D Concrete, IShape2D Shape, ICurve2D Curve, IGeometryValue2D Geometry);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConcreteFieldsOmitTheTagAndInterfaceFieldsKeepIt(bool authored)
    {
        var circle = Circle2D.Builder.Create(3, new(1, 2));
        var line = LineSegmentCurve2D.Builder.Create(Vector2f.Zero, Vector2f.One);
        var fields = new Fields(circle, circle, line, circle);
        var options = authored ? JsonProfiles.Objects : GeometryJson2D.JsonOptions;
        var json = JsonSerializer.Serialize(fields, options);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.False(root.GetProperty("concrete").TryGetProperty("kind", out _));
        Assert.Equal("circle", root.GetProperty("shape").GetProperty("kind").GetString());
        Assert.Equal("line", root.GetProperty("curve").GetProperty("kind").GetString());
        Assert.Equal("circle", root.GetProperty("geometry").GetProperty("kind").GetString());
        var restored = JsonSerializer.Deserialize<Fields>(json, options)!;
        Assert.Equal(circle, restored.Concrete);
        Assert.Equal(circle, Assert.IsType<Circle2D>(restored.Shape));
        Assert.Equal(line, Assert.IsType<LineSegmentCurve2D>(restored.Curve));
        Assert.Equal(circle, Assert.IsType<Circle2D>(restored.Geometry));
    }

    [Fact]
    public void ConcreteCompositesKeepTagsOnTheirPolymorphicChildren()
    {
        var composite = CompositeShape2D.Builder.Create([Circle2D.Builder.Create(1), Rectangle2D.Builder.FromSize(2, 3)]);
        var json = composite.ToJson();
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("kind", out _));
        var parts = document.RootElement.GetProperty("parts");
        Assert.Equal("circle", parts[0].GetProperty("kind").GetString());
        Assert.Equal("rectangle", parts[1].GetProperty("kind").GetString());
        var restored = JsonSerializer.Deserialize<CompositeShape2D>(json, GeometryJson2D.JsonOptions);
        Assert.IsType<Circle2D>(restored.Parts[0]);
        Assert.IsType<Rectangle2D>(restored.Parts[1]);
        Assert.Equal(composite.Area, restored.Area);
    }
}
