using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Json;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Geometry;

public sealed class ConcreteGeometryJsonTests
{
    public static TheoryData<string> Kinds => [.. ShapeKinds2D.All, .. CurveKinds2D.All];

    [Theory]
    [MemberData(nameof(Kinds))]
    public void ConcreteTypesRoundTripWithoutAKindTag(string kind)
    {
        var original = GeometryJson2D.CreateDefault(kind);
        var type = original.GetType();
        var json = JsonSerializer.Serialize(original, type, GeometryJson2D.JsonOptions);
        using var document = JsonDocument.Parse(json);
        Assert.False(document.RootElement.TryGetProperty("kind", out _));
        var restored = Assert.IsAssignableFrom<IGeometryValue2D>(JsonSerializer.Deserialize(json, type, GeometryJson2D.JsonOptions));
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
        Assert.Equal(ShapeKinds2D.Circle, polymorphic.RootElement.GetProperty("kind").GetString());
        Assert.Equal(circle, JsonSerializer.Deserialize<Circle2D>(circle.ToJson(), GeometryJson2D.JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Rectangle2D>(polymorphic.RootElement.GetRawText(), GeometryJson2D.JsonOptions));
    }

    private sealed record Fields(Circle2D Concrete, IShape2D Shape, ICurve2D Curve, IGeometryValue2D Geometry);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConcreteFieldsOmitTheTagAndInterfaceFieldsKeepIt(bool authored)
    {
        var circle = Circle2D.Builder.Create(3, new(1, 2));
        var line = LineSegmentCurve2D.Builder.Create(Vector2.Zero, Vector2.One);
        var fields = new Fields(circle, circle, line, circle);
        var options = authored ? AuthoredJson.Options : GeometryJson2D.JsonOptions;
        var json = JsonSerializer.Serialize(fields, options);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.False(root.GetProperty("concrete").TryGetProperty("kind", out _));
        Assert.Equal(ShapeKinds2D.Circle, root.GetProperty("shape").GetProperty("kind").GetString());
        Assert.Equal(CurveKinds2D.Line, root.GetProperty("curve").GetProperty("kind").GetString());
        Assert.Equal(ShapeKinds2D.Circle, root.GetProperty("geometry").GetProperty("kind").GetString());
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
        Assert.Equal(ShapeKinds2D.Circle, parts[0].GetProperty("kind").GetString());
        Assert.Equal(ShapeKinds2D.Rectangle, parts[1].GetProperty("kind").GetString());
        var restored = JsonSerializer.Deserialize<CompositeShape2D>(json, GeometryJson2D.JsonOptions);
        Assert.IsType<Circle2D>(restored.Parts[0]);
        Assert.IsType<Rectangle2D>(restored.Parts[1]);
        Assert.Equal(composite.Area, restored.Area);
    }
}
