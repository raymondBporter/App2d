using App2d.Core.Geometry.Builders;
using App2d.Core.Geometry.Authoring;
using App2d.Core.Geometry;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class ShapeBuilderAuthoringTests
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
        var source = Rectangle2D.Builder.FromSize(4, 2, new(1, 1));
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
        var tall = Rectangle2D.Builder.FromSize(1, 3);
        var capsule = Assert.IsType<Capsule2D.Builder>(tall.WithKind(ShapeKinds2D.Capsule));
        Assert.Equal(new Vector2(0, -1), capsule.Start);
        Assert.Equal(new Vector2(0, 1), capsule.End);
        Assert.Equal(.5f, capsule.Radius);
        Assert.Equal(1f, Assert.IsType<Circle2D.Builder>(tall.WithKind(ShapeKinds2D.Circle)).Radius * 2);
        Assert.Throws<ArgumentException>(() => tall.WithKind("blob"));
        Assert.Throws<InvalidOperationException>(() => new HalfSpace2D.Builder { Normal = new(0, 1), Offset = 0 }.WithKind(ShapeKinds2D.Circle));
    }

    [Fact]
    public void KindIsWrittenOnceAndTheAxisAlignedFlagOnlyWhenSet()
    {
        var plain = Rectangle2D.Builder.FromSize(1, 1).ToJson();
        Assert.Equal(1, plain.Split("\"kind\"").Length - 1);
        Assert.DoesNotContain("axisAligned", plain);
        var aligned = ShapeBuilder2D.FromShape(AxisAlignedRectangle2D.Builder.Create(new(0, 0), new(1, 1))).ToJson();
        Assert.Contains("\"axisAligned\": true", aligned);
        Assert.Equal(ShapeKinds2D.Rectangle, ShapeBuilder2D.FromJson(aligned).Kind);
    }

    [Fact]
    public void DefinitionsAreRecordsSoEditorsCanUseWithExpressions()
    {
        var circle = new Circle2D.Builder { Center = new(1, 2), Radius = 3 };
        var moved = circle with { Center = new(5, 5) };
        Assert.Equal(3f, moved.Radius);
        Assert.Equal(new Vector2(5, 5), Assert.IsType<Circle2D>(moved.Build()).Center);
        Assert.Equal(circle, new Circle2D.Builder { Center = new(1, 2), Radius = 3 });
        Assert.NotEqual(circle, moved);
    }
}
