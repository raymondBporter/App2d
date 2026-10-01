using App2d.Core.Geometry;
using App2d.Core;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class Rect2DTests
{
    private static readonly Rect2D Sample = new(new(-4, -2), new(2, 8));

    [Fact]
    public void ReportsDimensionsAndAllNineYUpAnchorPoints()
    {
        Assert.Equal(6, Sample.Width);
        Assert.Equal(10, Sample.Height);
        Assert.Equal(60, Sample.Area);
        Assert.Equal(new Vector2(6, 10), Sample.Size);
        Assert.Equal(new Vector2(3, 5), Sample.HalfSize);
        Assert.Equal(-4, Sample.Left);
        Assert.Equal(2, Sample.Right);
        Assert.Equal(-2, Sample.Bottom);
        Assert.Equal(8, Sample.Top);
        Assert.Equal(-1, Sample.MidX);
        Assert.Equal(3, Sample.MidY);
        Assert.Equal(new Vector2(-4, 8), Sample.TopLeft);
        Assert.Equal(new Vector2(-1, 8), Sample.TopCenter);
        Assert.Equal(new Vector2(2, 8), Sample.TopRight);
        Assert.Equal(new Vector2(-4, 3), Sample.CenterLeft);
        Assert.Equal(new Vector2(-1, 3), Sample.Center);
        Assert.Equal(new Vector2(2, 3), Sample.CenterRight);
        Assert.Equal(new Vector2(-4, -2), Sample.BottomLeft);
        Assert.Equal(new Vector2(-1, -2), Sample.BottomCenter);
        Assert.Equal(new Vector2(2, -2), Sample.BottomRight);
    }

    [Fact]
    public void ImplementingOnlyMinAndMaxGivesConcreteTypesAndInterfacesTheSameOperations()
    {
        var value = new CustomValue(Sample.Min, Sample.Max);
        var reference = new CustomReference(Sample.Min, Sample.Max);
        IRect2D contract = value;
        Assert.Equal(6, value.Width);
        Assert.Equal(new Vector2(-4, 8), reference.TopLeft);
        Assert.Equal(new Vector2(-4, 3), contract.CenterLeft);
        Assert.True(value.Contains(reference));
        Assert.True(reference.Intersects(value));
        Assert.True(contract.Contains(Sample.Center));
        Assert.Equal(Sample, contract.ToRect());
    }

    [Fact]
    public void BoundsAndBothRectangleShapesAdoptTheContractInTheirOwnCoordinateSpace()
    {
        var bounds = new Rect2D(Sample.Min, Sample.Max);
        var shape = new Rectangle2D(Sample.Min, Sample.Max);
        var axisAligned = new AxisAlignedRectangle2D(Sample.Min, Sample.Max);
        Assert.Equal(6, bounds.Width);
        Assert.Equal(10, shape.Height);
        Assert.Equal(new Vector2(2, 8), axisAligned.TopRight);
        Assert.True(bounds.Intersects(shape));
        Assert.True(shape.Contains(bounds));
        Assert.True(axisAligned.Contains(Sample));
        Assert.Equal(bounds, new SpatialObject2D(shape).LocalBounds);
        Assert.Equal(shape.Area, ((IRect2D)shape).Area);
        Assert.Equal(shape.ContainsPoint(Sample.Max), shape.Contains(Sample.Max));
        Assert.False(typeof(IShape2D).IsAssignableFrom(typeof(Rect2D)));
        Assert.False(typeof(IShape2D).IsAssignableFrom(typeof(IRect2D)));
    }

    [Theory]
    [InlineData(-4f, -2f, true)]
    [InlineData(2f, 8f, true)]
    [InlineData(-4f, 8f, true)]
    [InlineData(2f, -2f, true)]
    [InlineData(0f, 0f, true)]
    [InlineData(2.01f, 0f, false)]
    [InlineData(0f, 8.01f, false)]
    public void PointContainmentIncludesEdges(float x, float y, bool expected) =>
        Assert.Equal(expected, Sample.Contains(new Vector2(x, y)));

    [Fact]
    public void RectangleContainmentRequiresTheWholeOtherRectangle()
    {
        Assert.True(Sample.Contains(new Rect2D(new(-3, -1), new(1, 7))));
        Assert.True(Sample.Contains(Sample));
        var partial = new CustomValue(new(1, 7), new(3, 9));
        Assert.True(Sample.Intersects(partial));
        Assert.False(Sample.Contains(partial));
        Assert.False(partial.Contains(Sample));
    }

    [Theory]
    [InlineData(1f, 1f, true, 1f, 1f)]
    [InlineData(2f, 0f, true, 0f, 2f)]
    [InlineData(2f, 2f, true, 0f, 0f)]
    [InlineData(2.01f, 0f, false, 0f, 0f)]
    public void IntersectionDistinguishesDisjointRectsFromEdgeAndCornerContact(
        float x, float y, bool intersects, float width, float height)
    {
        var first = new Rect2D(Vector2.Zero, new(2));
        var second = new Rect2D(new(x, y), new(x + 2, y + 2));
        Assert.Equal(intersects, first.Intersects(second));
        Assert.Equal(intersects, second.Intersects(first));
        Assert.Equal(intersects, first.TryIntersect(second, out var result));
        Assert.Equal(width, result.Width);
        Assert.Equal(height, result.Height);
        if (intersects)
        {
            Assert.Equal(new Vector2(x, y), result.Min);
            Assert.True(first.Contains(result));
            Assert.True(second.Contains(result));
        }
        else Assert.Equal(default, result);
    }

    [Fact]
    public void UnionContainsBothRectsAndClosestPointClampsToTheirEdges()
    {
        var other = new CustomReference(new(0, -5), new(10, 4));
        var union = Sample.Union(other);
        Assert.Equal(new Rect2D(new(-4, -5), new(10, 8)), union);
        Assert.True(union.Contains(Sample));
        Assert.True(union.Contains(other));
        Assert.Equal(new Vector2(-4, 8), Sample.ClosestPoint(new(-10, 20)));
        Assert.Equal(new Vector2(0, 3), Sample.ClosestPoint(new(0, 3)));
    }

    [Fact]
    public void MovementExpansionAndInsetProduceNewValuesAndClampOversizedInsets()
    {
        Assert.Equal(new Rect2D(new(-3, 0), new(3, 10)), Sample.TranslatedBy(new(1, 2)));
        Assert.Equal(new Rect2D(new(-5, -4), new(3, 10)), Sample.InflatedBy(1, 2));
        Assert.Equal(new Rect2D(new(-3, 0), new(1, 6)), Sample.InsetBy(1, 2));
        Assert.Equal(new Rect2D(new(-1, 0), new(-1, 6)), Sample.InsetBy(100, 2));
        var collapsed = Sample.InsetBy(100, 100);
        Assert.Equal(Sample.Center, collapsed.Min);
        Assert.Equal(Sample.Center, collapsed.Max);
        Assert.Equal(new Vector2(-4, -2), Sample.Min);
        Assert.Equal(new Vector2(2, 8), Sample.Max);
    }

    [Fact]
    public void DefaultAndZeroSizeRectsAreValidPointRects()
    {
        var point = default(Rect2D);
        Assert.Equal(0, point.Area);
        Assert.True(point.Contains(Vector2.Zero));
        Assert.False(point.Contains(Vector2.One));
        Assert.True(point.Intersects(point));
        Assert.Equal(new Rect2D(new(3, 4), new(3, 4)), Rect2D.FromSize(default, new(3, 4)));
    }

    [Fact]
    public void InfiniteBoundsKeepTheirBroadPhaseBehavior()
    {
        var unbounded = Rect2D.Unbounded;
        Assert.False(unbounded.IsFinite);
        Assert.False(unbounded.ToRect().IsFinite);
        Assert.True(unbounded.Contains(Sample));
        Assert.True(unbounded.Intersects(Sample));
        Assert.True(unbounded.TryIntersect(Sample, out var finite));
        Assert.Equal(Sample, finite);
        Assert.Equal(unbounded.ToRect(), Sample.Union(unbounded));
        Assert.Equal(unbounded, unbounded.TransformedBy(Matrix3x2.Identity));
        Assert.True(unbounded.Intersects(new Rect2D(Sample.Min, Sample.Max)));
    }

    [Fact]
    public void ConstructionAndInsetsRejectInvalidInputs()
    {
        Assert.Throws<ArgumentException>(() => new Rect2D(Vector2.One, Vector2.Zero));
        Assert.Throws<ArgumentException>(() => new Rect2D(new(float.NaN, 0), Vector2.One));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rect2D.FromSize(new(-1, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Sample.InflatedBy(-1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => Sample.InsetBy(1, float.NaN));
        Assert.Equal(Sample, Rect2D.FromSize(new(6, 10), new(-1, 3)));
        Assert.Equal(Sample, Rect2D.FromPoints(Sample.Max, Sample.Min));
    }

    [Fact]
    public void CustomValueRectQueriesAndCornerWritingDoNotBoxOrAllocate()
    {
        var value = new CustomValue(Sample.Min, Sample.Max);
        Span<Vector2> corners = stackalloc Vector2[4];
        for (var i = 0; i < 100; i++) _ = Query(value, corners);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var total = 0f;
        for (var i = 0; i < 1000; i++) total += Query(value, corners);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.True(total > 0);
        Assert.Equal(Sample.BottomLeft, corners[0]);
        Assert.Equal(Sample.BottomRight, corners[1]);
        Assert.Equal(Sample.TopRight, corners[2]);
        Assert.Equal(Sample.TopLeft, corners[3]);

        static float Query<T>(T rectangle, Span<Vector2> corners) where T : IRect2D
        {
            rectangle.WriteCorners(corners);
            return rectangle.Contains(rectangle.Center) && rectangle.TryIntersect(rectangle, out var overlap)
                ? overlap.Area + rectangle.Width + rectangle.TopLeft.Y : 0;
        }
    }

    private readonly record struct CustomValue(Vector2 Min, Vector2 Max) : IRect2D;
    private sealed record CustomReference(Vector2 Min, Vector2 Max) : IRect2D;
}
