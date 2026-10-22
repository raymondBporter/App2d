using App2d.Core.Geometry;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json;
using static App2d.Tests.Geometry.VectorComparers;

namespace App2d.Tests.Geometry;

public sealed class RectangleJsonTests
{
    [Fact]
    public void WritesCenterSizeAndAngle()
    {
        var json = Rectangle2D.Builder.FromSize(4, 2, new(1, 1), .5f).ToJson();
        Assert.Contains("\"center\"", json);
        Assert.Contains("\"size\"", json);
        Assert.Equal(.5f, JsonDocument.Parse(json).RootElement.GetProperty("angleRadians").GetSingle(), 5);
        Assert.DoesNotContain("\"min\"", json);
        var back = Assert.IsType<Rectangle2D>(GeometryJson2D.ShapeFromJson(json).Validated());
        Assert.Equal(.5f, back.AngleRadians, 5);
        Assert.Equal(new Vector2(4, 2), back.Size, Near);
    }

    [Fact]
    public void MinMaxSpellingReadsAxisAligned()
    {
        var box = Assert.IsType<Rectangle2D>(GeometryJson2D.ShapeFromJson("{\"kind\":\"rectangle\",\"min\":{\"x\":-1,\"y\":-2},\"max\":{\"x\":3,\"y\":1}}").Validated());
        Assert.Equal(new Vector2(1, -.5f), box.Center);
        Assert.Equal(new Vector2(2, 1.5f), box.HalfSize);
        Assert.True(box.IsAxisAligned);
    }

    [Fact]
    public void MinMaxSpellingAcceptsAnAngle()
    {
        var box = Assert.IsType<Rectangle2D>(GeometryJson2D.ShapeFromJson("{\"kind\":\"rectangle\",\"min\":{\"x\":-1,\"y\":-2},\"max\":{\"x\":3,\"y\":1},\"angleRadians\":1.5707963}").Validated());
        Assert.Equal(new Vector2(1, -.5f), box.Center);
        Assert.Equal(Vector2.UnitY, box.Axis, Near);
    }

    [Fact]
    public void MixedRectangleSpellingsAreRejected() =>
        Assert.Throws<JsonException>(() => GeometryJson2D.ShapeFromJson("{\"kind\":\"rectangle\",\"min\":{\"x\":0,\"y\":0},\"max\":{\"x\":1,\"y\":1},\"center\":{\"x\":0,\"y\":0},\"size\":{\"x\":1,\"y\":1}}"));

    [Fact]
    public void BackwardsMinMaxStillFailsValidation() =>
        Assert.ThrowsAny<ArgumentException>(() => GeometryJson2D.ShapeFromJson("{\"kind\":\"rectangle\",\"min\":{\"x\":1,\"y\":1},\"max\":{\"x\":0,\"y\":0}}").Validated());

    [Fact]
    public void AxisAlignedKindStillReadsMinMax() =>
        Assert.IsType<AxisAlignedRectangle2D>(GeometryJson2D.ShapeFromJson("{\"kind\":\"axis-aligned-rectangle\",\"min\":{\"x\":0,\"y\":0},\"max\":{\"x\":1,\"y\":1}}").Validated());
}
