using App2d.Core.Geometry;
using App2d.Core.Geometry.Functions;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class Clipping2DTests
{
    private static readonly Rect2D Rectangle = new(new(-2, -1), new(2, 1));

    [Fact]
    public void LineClipsToRectangleIncludingAnEdge()
    {
        Assert.True(new Line2D(new(-5, 0), Vector2.UnitX)
            .TryClipToRectangle(Rectangle, out var start, out var end));
        Assert.Equal(new Vector2(-2, 0), start);
        Assert.Equal(new Vector2(2, 0), end);

        Assert.True(new Line2D(new(0, 1), Vector2.UnitX)
            .TryClipToRectangle(Rectangle, out start, out end));
        Assert.Equal(new Vector2(-2, 1), start);
        Assert.Equal(new Vector2(2, 1), end);
    }

    [Fact]
    public void LineCanMissOrTouchOnlyOneCorner()
    {
        Assert.False(new Line2D(new(0, 2), Vector2.UnitX)
            .TryClipToRectangle(Rectangle, out _, out _));
        Assert.True(new Line2D(new(-2, -1), new(1, -1))
            .TryClipToRectangle(Rectangle, out var start, out var end));
        Assert.Equal(new Vector2(-2, -1), start);
        Assert.Equal(start, end);
    }

    [Fact]
    public void RayClipRespectsItsOriginAndDirection()
    {
        Assert.True(new Ray2D(Vector2.Zero, Vector2.UnitX)
            .TryClipToRectangle(Rectangle, out var start, out var end));
        Assert.Equal(Vector2.Zero, start);
        Assert.Equal(new Vector2(2, 0), end);

        Assert.True(new Ray2D(new(-3, 0), Vector2.UnitX)
            .TryClipToRectangle(Rectangle, out start, out end));
        Assert.Equal(new Vector2(-2, 0), start);
        Assert.Equal(new Vector2(2, 0), end);
        Assert.False(new Ray2D(new(-3, 0), -Vector2.UnitX)
            .TryClipToRectangle(Rectangle, out _, out _));
    }

    [Fact]
    public void RectangleHalfSpaceClipReturnsOnlyTheVisiblePolygon()
    {
        Span<Vector2> output = stackalloc Vector2[5];
        var count = PolygonClipping2D.ClipRectangleToHalfSpace(Rectangle,
            new HalfSpace2D(Vector2.UnitX, 0), output);
        Assert.Equal(4, count);
        Assert.Equal(new Vector2(-2, -1), output[0]);
        Assert.Equal(new Vector2(0, -1), output[1]);
        Assert.Equal(new Vector2(0, 1), output[2]);
        Assert.Equal(new Vector2(-2, 1), output[3]);

        Assert.Equal(4, PolygonClipping2D.ClipRectangleToHalfSpace(Rectangle,
            new HalfSpace2D(Vector2.UnitX, 10), output));
        Assert.Equal(0, PolygonClipping2D.ClipRectangleToHalfSpace(Rectangle,
            new HalfSpace2D(Vector2.UnitX, -10), output));
    }

    [Fact]
    public void DiagonalClipCanProduceFiveVerticesWithoutAllocating()
    {
        Span<Vector2> output = stackalloc Vector2[5];
        var halfSpace = new HalfSpace2D(new(1, 1), 2);
        var count = PolygonClipping2D.ClipRectangleToHalfSpace(Rectangle, halfSpace, output);
        Assert.Equal(5, count);
        foreach (var point in output[..count]) Assert.True(halfSpace.ContainsPoint(point));
    }

    [Fact]
    public void ConvexClipKeepsBoundaryVerticesOnce()
    {
        Vector2[] triangle = [new(-2, 0), new(2, 0), new(0, 2)];
        Span<Vector2> output = stackalloc Vector2[4];
        var count = PolygonClipping2D.ClipConvexToHalfSpace(triangle,
            new HalfSpace2D(Vector2.UnitX, 0), output);
        Assert.Equal(3, count);
        Assert.Equal(new Vector2(-2, 0), output[0]);
        Assert.Equal(new Vector2(0, 0), output[1]);
        Assert.Equal(new Vector2(0, 2), output[2]);
    }

    [Fact]
    public void ClippingRejectsUnboundedRectanglesAndShortOutput()
    {
        var unbounded = new Rect2D(new(float.NegativeInfinity, -1), new(float.PositiveInfinity, 1));
        Assert.Throws<ArgumentException>(() => new Line2D(Vector2.Zero, Vector2.UnitX)
            .TryClipToRectangle(unbounded, out _, out _));
        Assert.Throws<ArgumentException>(() => PolygonClipping2D.ClipRectangleToHalfSpace(unbounded,
            new HalfSpace2D(Vector2.UnitX, 0), new Vector2[5]));
        Assert.Throws<ArgumentException>(() => PolygonClipping2D.ClipRectangleToHalfSpace(Rectangle,
            new HalfSpace2D(Vector2.UnitX, 0), new Vector2[4]));
    }
}
