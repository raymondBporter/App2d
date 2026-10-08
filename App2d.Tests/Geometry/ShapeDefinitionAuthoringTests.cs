using App2d.Core.Geometry;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class ShapeDefinitionAuthoringTests
{
    [Theory]
    [InlineData(ShapeKinds2D.Rectangle)]
    [InlineData(ShapeKinds2D.Circle)]
    [InlineData(ShapeKinds2D.Capsule)]
    [InlineData(ShapeKinds2D.Ellipse)]
    [InlineData(ShapeKinds2D.Triangle)]
    [InlineData(ShapeKinds2D.ConvexPolygon)]
    [InlineData(ShapeKinds2D.Composite)]
    public void ReauthoringFitsTheNewKindInsideTheOriginalBoundsAroundTheSameCentre(string kind)
    {
        var source = RectangleShapeDefinition2D.FromSize(new(4, 2), new(1, 1));
        var converted = source.WithKind(kind);
        Assert.Equal(kind, converted.Kind);
        var bounds = ShapeBounds2D.Calculate(converted.Build());
        Assert.True(new Rect2D(new(-1, 0), new(3, 2)).InflatedBy(1e-5f, 1e-5f).Contains(bounds), $"{kind} spilled to {bounds}.");
        Assert.True(Vector2.Distance(new(1, 1), bounds.Center) < 1e-5f);
        Assert.Same(source, source.WithKind(ShapeKinds2D.Rectangle));
    }

    [Fact]
    public void TallBoxesBecomeVerticalCapsulesAndUnknownOrUnboundedSourcesAreRejected()
    {
        var tall = RectangleShapeDefinition2D.FromSize(new(1, 3));
        var capsule = Assert.IsType<CapsuleShapeDefinition2D>(tall.WithKind(ShapeKinds2D.Capsule));
        Assert.Equal(new Vector2(0, -1), capsule.Start);
        Assert.Equal(new Vector2(0, 1), capsule.End);
        Assert.Equal(.5f, capsule.Radius);
        Assert.Equal(1f, Assert.IsType<CircleShapeDefinition2D>(tall.WithKind(ShapeKinds2D.Circle)).Radius * 2);
        Assert.Throws<ArgumentException>(() => tall.WithKind("blob"));
        Assert.Throws<InvalidOperationException>(() => new HalfSpaceShapeDefinition2D { Normal = new(0, 1), Offset = 0 }.WithKind(ShapeKinds2D.Circle));
    }

    [Fact]
    public void KindIsWrittenOnceAndTheAxisAlignedFlagOnlyWhenSet()
    {
        var plain = RectangleShapeDefinition2D.FromSize(new(1, 1)).ToJson();
        Assert.Equal(1, plain.Split("\"kind\"").Length - 1);
        Assert.DoesNotContain("axisAligned", plain);
        var aligned = ShapeDefinition2D.FromShape(new AxisAlignedRectangle2D(new(0, 0), new(1, 1))).ToJson();
        Assert.Contains("\"axisAligned\": true", aligned);
        Assert.Equal(ShapeKinds2D.Rectangle, ShapeDefinition2D.FromJson(aligned).Kind);
    }

    [Fact]
    public void DefinitionsAreRecordsSoEditorsCanUseWithExpressions()
    {
        var circle = new CircleShapeDefinition2D { Center = new(1, 2), Radius = 3 };
        var moved = circle with { Center = new(5, 5) };
        Assert.Equal(3f, moved.Radius);
        Assert.Equal(new Vector2(5, 5), Assert.IsType<Circle2D>(moved.Build()).Center);
        Assert.Equal(circle, new CircleShapeDefinition2D { Center = new(1, 2), Radius = 3 });
        Assert.NotEqual(circle, moved);
    }
}
