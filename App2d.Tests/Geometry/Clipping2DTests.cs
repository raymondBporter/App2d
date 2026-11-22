using App2d.Core.Geometry.Clipping;
using App2d.Core.Geometry.Intersection;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;

namespace App2d.Tests.Geometry;

public sealed class Clipping2DTests
{
    private static readonly Rect2f Rectangle = Rect2f.Builder.Create(new(-2, -1), new(2, 1));

    [Fact]
    public void LineClipsToRectangleIncludingAnEdge()
    {
        Assert.True(new Line2D(new(-5, 0), Vector2f.UnitX)
            .TryClipToRectangle(Rectangle, out var start, out var end));
        Assert.Equal(new Vector2f(-2, 0), start);
        Assert.Equal(new Vector2f(2, 0), end);

        Assert.True(new Line2D(new(0, 1), Vector2f.UnitX)
            .TryClipToRectangle(Rectangle, out start, out end));
        Assert.Equal(new Vector2f(-2, 1), start);
        Assert.Equal(new Vector2f(2, 1), end);
    }

    [Fact]
    public void LineCanMissOrTouchOnlyOneCorner()
    {
        Assert.False(new Line2D(new(0, 2), Vector2f.UnitX)
            .TryClipToRectangle(Rectangle, out _, out _));
        Assert.True(new Line2D(new(-2, -1), new(1, -1))
            .TryClipToRectangle(Rectangle, out var start, out var end));
        Assert.Equal(new Vector2f(-2, -1), start);
        Assert.Equal(start, end);
    }

    [Fact]
    public void RayClipRespectsItsOriginAndDirection()
    {
        Assert.True(new Ray2D(Vector2f.Zero, Vector2f.UnitX)
            .TryClipToRectangle(Rectangle, out var start, out var end));
        Assert.Equal(Vector2f.Zero, start);
        Assert.Equal(new Vector2f(2, 0), end);

        Assert.True(new Ray2D(new(-3, 0), Vector2f.UnitX)
            .TryClipToRectangle(Rectangle, out start, out end));
        Assert.Equal(new Vector2f(-2, 0), start);
        Assert.Equal(new Vector2f(2, 0), end);
        Assert.False(new Ray2D(new(-3, 0), -Vector2f.UnitX)
            .TryClipToRectangle(Rectangle, out _, out _));
    }

    [Fact]
    public void RectangleHalfSpaceClipReturnsOnlyTheVisiblePolygon()
    {
        Span<Vector2f> output = stackalloc Vector2f[5];
        var count = PolygonClipping2D.ClipRectangleToHalfSpace(Rectangle, Vector2f.UnitX, 0, output);
        Assert.Equal(4, count);
        Assert.Equal(new Vector2f(-2, -1), output[0]);
        Assert.Equal(new Vector2f(0, -1), output[1]);
        Assert.Equal(new Vector2f(0, 1), output[2]);
        Assert.Equal(new Vector2f(-2, 1), output[3]);

        Assert.Equal(4, PolygonClipping2D.ClipRectangleToHalfSpace(Rectangle, Vector2f.UnitX, 10, output));
        Assert.Equal(0, PolygonClipping2D.ClipRectangleToHalfSpace(Rectangle, Vector2f.UnitX, -10, output));
    }

    [Fact]
    public void DiagonalClipCanProduceFiveVerticesWithoutAllocating()
    {
        Span<Vector2f> output = stackalloc Vector2f[5];
        var halfSpace = HalfSpace2D.Builder.Create(new(1, 1), 2);
        var count = PolygonClipping2D.ClipRectangleToHalfSpace(Rectangle, halfSpace.Normal, halfSpace.Offset, output);
        Assert.Equal(5, count);
        foreach (var point in output[..count])
        {
            Assert.True(halfSpace.ContainsPoint(point));
        }
    }

    [Fact]
    public void ConvexClipKeepsBoundaryVerticesOnce()
    {
        Vector2f[] triangle = [new(-2, 0), new(2, 0), new(0, 2)];
        Span<Vector2f> output = stackalloc Vector2f[4];
        var count = PolygonClipping2D.ClipConvexToHalfSpace(triangle, Vector2f.UnitX, 0, output);
        Assert.Equal(3, count);
        Assert.Equal(new Vector2f(-2, 0), output[0]);
        Assert.Equal(new Vector2f(0, 0), output[1]);
        Assert.Equal(new Vector2f(0, 2), output[2]);
    }

    [Fact]
    public void ClippingRejectsUnboundedRectanglesAndShortOutput()
    {
        var unbounded = Rect2f.Builder.CreateUnbounded(new(float.NegativeInfinity, -1), new(float.PositiveInfinity, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Line2D(Vector2f.Zero, Vector2f.UnitX)
            .TryClipToRectangle(unbounded, out _, out _));
        Assert.ThrowsAny<ArgumentException>(() => PolygonClipping2D.ClipRectangleToHalfSpace(unbounded, Vector2f.UnitX, 0, new Vector2f[5]));
        Assert.ThrowsAny<ArgumentException>(() => PolygonClipping2D.ClipRectangleToHalfSpace(Rectangle, Vector2f.UnitX, 0, new Vector2f[4]));
    }
}
