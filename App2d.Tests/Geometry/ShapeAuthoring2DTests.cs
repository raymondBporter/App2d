using App2d.Core.Geometry;
using App2d.Core.Geometry.Operations.Bounds;
using App2d.Core.Geometry.Operations.Conversion;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Serialization;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Geometry;

public sealed class ShapeAuthoring2DTests
{
    [Theory]
    [InlineData(ShapeKinds2D.Rectangle)]
    [InlineData(ShapeKinds2D.AxisAlignedRectangle)]
    [InlineData(ShapeKinds2D.Circle)]
    [InlineData(ShapeKinds2D.Capsule)]
    [InlineData(ShapeKinds2D.Ellipse)]
    [InlineData(ShapeKinds2D.Triangle)]
    [InlineData(ShapeKinds2D.ConvexPolygon)]
    [InlineData(ShapeKinds2D.Composite)]
    public void ReauthoringFitsTheNewKindInsideTheOriginalBoundsAroundTheSameCentre(string kind)
    {
        var source = Rectangle2D.Builder.FromSize(4, 2, new(1, 1));
        var converted = source.WithKind(kind);
        Assert.Equal(kind, converted.Kind);
        var bounds = ShapeBounds2D.Calculate(converted.Validated());
        Assert.True(new Rect2D(new(-1, 0), new(3, 2)).InflatedBy(1e-5f, 1e-5f).Contains(bounds), $"{kind} spilled to {bounds}.");
        Assert.True(Vector2.Distance(new(1, 1), bounds.Center) < 1e-5f);
        Assert.Equal(source, source.WithKind(ShapeKinds2D.Rectangle));
    }

    [Fact]
    public void TallBoxesBecomeVerticalCapsulesAndUnknownOrUnboundedSourcesAreRejected()
    {
        var tall = Rectangle2D.Builder.FromSize(1, 3);
        var capsule = Assert.IsType<Capsule2D>(tall.WithKind(ShapeKinds2D.Capsule));
        Assert.Equal(new Vector2(0, -1), capsule.Start);
        Assert.Equal(new Vector2(0, 1), capsule.End);
        Assert.Equal(.5f, capsule.Radius);
        Assert.Equal(1f, Assert.IsType<Circle2D>(tall.WithKind(ShapeKinds2D.Circle)).Radius * 2);
        Assert.Throws<ArgumentException>(() => tall.WithKind("blob"));
        Assert.Throws<InvalidOperationException>(() => new HalfSpace2D { Normal = new(0, 1), Offset = 0 }.Validated().WithKind(ShapeKinds2D.Circle));
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
            Assert.Equal(rectangle.Kind, document.RootElement.GetProperty("kind").GetString());
            Assert.False(document.RootElement.TryGetProperty("axisAligned", out _));
            Assert.Equal(rectangle.GetType(), GeometryJson2D.ShapeFromJson(json).Validated().GetType());
            Assert.Equal(rectangle.GetType(), Assert.IsType<IShape2D>(GeometryJson2D.FromJson(json), exactMatch: false).Validated().GetType());
        }
    }

    [Theory]
    [InlineData(ShapeKinds2D.Rectangle)]
    [InlineData(ShapeKinds2D.AxisAlignedRectangle)]
    public void RectangleJsonDefersValidationAndRejectsTheRemovedFlag(string kind)
    {
        var backwards = "{\"kind\":\"" + kind + "\",\"min\":{\"x\":1,\"y\":1},\"max\":{\"x\":0,\"y\":0}}";
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
        Assert.Equal(new Vector2(5, 5), Assert.IsType<Circle2D>(moved.Validated()).Center);
        Assert.Equal(circle, new Circle2D { Center = new(1, 2), Radius = 3 }.Validated());
        Assert.NotEqual(circle, moved);
    }
}
