using App2d.Core.Geometry;
using App2d.Core.Geometry.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using System.Text.Json;

namespace App2d.Tests.Geometry;

public sealed class ShapeAuthoring2DTests
{
    [Theory]
    [InlineData(GeometryKind.Rectangle)]
    [InlineData(GeometryKind.AxisAlignedRectangle)]
    [InlineData(GeometryKind.Circle)]
    [InlineData(GeometryKind.Capsule)]
    [InlineData(GeometryKind.Ellipse)]
    [InlineData(GeometryKind.Triangle)]
    [InlineData(GeometryKind.ConvexPolygon)]
    [InlineData(GeometryKind.Composite)]
    public void ReauthoringFitsTheNewKindInsideTheOriginalBoundsAroundTheSameCentre(GeometryKind kind)
    {
        var source = Rectangle2D.Builder.FromSize(4, 2, new(1, 1));
        var converted = source.WithKind(kind);
        Assert.Equal(kind, converted.Kind);
        var bounds = ShapeBounds2D.Calculate(converted.Validated());
        Assert.True(Rect2f.Builder.Create(new(-1, 0), new(3, 2)).InflatedBy(1e-5f, 1e-5f).Contains(bounds), $"{kind} spilled to {bounds}.");
        Assert.True(Vector2f.Distance(new(1, 1), bounds.Center) < 1e-5f);
        Assert.Equal(source, source.WithKind(source.Kind));
    }

    [Fact]
    public void TallBoxesBecomeVerticalCapsulesAndUnknownOrUnboundedSourcesAreRejected()
    {
        var tall = Rectangle2D.Builder.FromSize(1, 3);
        var capsule = Assert.IsType<Capsule2>(tall.WithKind(GeometryKind.Capsule));
        Assert.Equal(new Vector2f(0, -1), capsule.Start);
        Assert.Equal(new Vector2f(0, 1), capsule.End);
        Assert.Equal(.5f, capsule.Radius);
        Assert.Equal(1f, Assert.IsType<Circle2D>(tall.WithKind(GeometryKind.Circle)).Radius * 2);
        Assert.Throws<ArgumentException>(() => tall.WithKind(GeometryKind.Custom));
        Assert.Throws<InvalidOperationException>(() => new HalfSpace2D { Normal = new(0, 1), Offset = 0 }.Validated().WithKind(GeometryKind.Circle));
    }

    [Fact]
    public void RectangleKindsDistinguishTheirTypesWithoutASecondFlag()
    {
        Rectangle2D plain = Rectangle2D.Builder.FromSize(1, 1).Validated();
        AxisAlignedRectangle2D aligned = AxisAlignedRectangle2D.Builder.Create(1, 1).Validated();
        foreach (var rectangle in new IConvexShape2D[] { plain, aligned })
        {
            var builder = rectangle;
            var json = builder.ToJson();
            using var document = JsonDocument.Parse(json);
            Assert.Single(document.RootElement.EnumerateObject(), property => property.Name == "kind");
            Assert.Equal(rectangle.Kind, document.RootElement.GetProperty("kind").Deserialize<GeometryKind>());
            Assert.False(document.RootElement.TryGetProperty("axisAligned", out _));
            Assert.Equal(rectangle.GetType(), GeometryJson2D.ShapeFromJson(json).Validated().GetType());
            Assert.Equal(rectangle.GetType(), Assert.IsType<IShape2D>(GeometryJson2D.FromJson(json), exactMatch: false).Validated().GetType());
        }
    }

    [Theory]
    [InlineData(GeometryKind.Rectangle)]
    [InlineData(GeometryKind.AxisAlignedRectangle)]
    public void RectangleJsonDefersValidationAndRejectsTheRemovedFlag(GeometryKind kind)
    {
        var backwards = "{\"kind\":" + JsonSerializer.Serialize(kind) + ",\"min\":{\"x\":1,\"y\":1},\"max\":{\"x\":0,\"y\":0}}";
        var builder = GeometryJson2D.ShapeFromJson(backwards);
        Assert.Equal(kind, builder.Kind);
        Assert.ThrowsAny<ArgumentException>(builder.Validate);
        Assert.ThrowsAny<ArgumentException>(() => builder.Validated());
        var withFlag = backwards[..^1] + ",\"axisAligned\":true}";
        Assert.Throws<JsonException>(() => GeometryJson2D.ShapeFromJson(withFlag));
        Assert.Throws<JsonException>(() => GeometryJson2D.FromJson(withFlag));
    }

    [Fact]
    public void ValuesAreRecordsSoEditorsCanUseWithExpressions()
    {
        var circle = new Circle2D { Center = new(1, 2), Radius = 3 }.Validated();
        var moved = circle with
        {
            Center = new(5, 5)
        };
        Assert.Equal(3f, moved.Radius);
        Assert.Equal(new Vector2f(5, 5), Assert.IsType<Circle2D>(moved.Validated()).Center);
        Assert.Equal(circle, new Circle2D { Center = new(1, 2), Radius = 3 }.Validated());
        Assert.NotEqual(circle, moved);
    }
}
