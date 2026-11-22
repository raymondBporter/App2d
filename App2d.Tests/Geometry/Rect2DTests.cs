using App2d.Core.Geometry.Bounds;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Shapes;
using App2d.Core.Spatial;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Geometry;

public sealed class Rect2DTests
{
    [Fact]
    public void InfiniteEdgesRequireExplicitConstructionAndMustPointOutward()
    {
        var min = new Vector2f(float.NegativeInfinity, 2);
        var max = new Vector2f(5, float.PositiveInfinity);
        Assert.ThrowsAny<ArgumentException>(() => Rect2f.Builder.Create(min, max));
        var bounds = Rect2f.Builder.CreateUnbounded(min, max);
        Assert.Equal(min, bounds.Min);
        Assert.Equal(max, bounds.Max);
        bounds.Validate(allowUnbounded: true);
        Assert.ThrowsAny<ArgumentException>(() => bounds.Validate());
        Assert.ThrowsAny<ArgumentException>(() => Rect2f.Builder.CreateUnbounded(new(float.NaN, 0), max));
        Assert.ThrowsAny<ArgumentException>(() => Rect2f.Builder.CreateUnbounded(new(float.PositiveInfinity, 0), Vector2f.PositiveInfinity));
        Assert.ThrowsAny<ArgumentException>(() => Rect2f.Builder.CreateUnbounded(max, min));
        Assert.ThrowsAny<ArgumentException>(() => Rect2d.Builder.Create(Vector2d.Zero, new(1, double.PositiveInfinity)));
    }

    [Fact]
    public void JsonCanReadTheWholeRectangleListBeforeValidation()
    {
        const string json = """[{"Min":{"X":4,"Y":0},"Max":{"X":2,"Y":1}},{"Min":{"X":1,"Y":0},"Max":{"X":3,"Y":1}}]""";
        var boxes = JsonSerializer.Deserialize<Rect2f[]>(json)!;
        Assert.Equal(4, boxes[0].Min.X);
        Assert.ThrowsAny<ArgumentException>(() => boxes[0].Validate());
        boxes[1].Validate();
        Assert.Equal(3, boxes[1].Max.X);
        var doubles = JsonSerializer.Deserialize<Rect2d[]>(json)!;
        Assert.ThrowsAny<ArgumentException>(() => doubles[0].Validate());
        doubles[1].Validate();
    }

    private static readonly Rect2f Sample = Rect2f.Builder.Create(new(-4, -2), new(2, 8));

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(-4, 0, false)]
    [InlineData(2, 0, false)]
    [InlineData(0, -2, false)]
    [InlineData(0, 8, false)]
    [InlineData(-4, -2, false)]
    [InlineData(0, 9, false)]
    public void InteriorContainmentExcludesEveryBoundary(float x, float y, bool expected)
    {
        var point = new Vector2f(x, y);
        Assert.Equal(expected, Sample.ContainsInterior(point));
        Assert.Equal(expected, new CustomValue(Sample.Min, Sample.Max).ContainsInterior(point));
    }

    [Fact]
    public void DegenerateRectanglesHaveNoInteriorAndNaNIsNeverContained()
    {
        Assert.False(default(Rect2f).ContainsInterior(Vector2f.Zero));
        Assert.False(Rect2f.Builder.Create(new(0, -1), new(0, 1)).ContainsInterior(Vector2f.Zero));
        Assert.False(Sample.ContainsInterior(new(float.NaN, 0)));
        Assert.True(Rect2f.Unbounded.ContainsInterior(Vector2f.Zero));
        Assert.False(Rect2f.Unbounded.ContainsInterior(new(float.PositiveInfinity, 0)));
    }

    [Fact]
    public void PositiveSizesCreateCenteredAndMinAnchoredRectangles()
    {
        var size = new Size2D(6f, 10f);
        Assert.Equal(Sample, Rect2f.FromSize(size, new(-1f, 3f)));
        Assert.Equal(Sample, Rect2f.FromMinAndSize(Sample.Min, size));
        Assert.Equal(Sample, ShapeBounds2D.Calculate(Rectangle2D.Builder.CreateFromSize(size, new(-1f, 3f))));
        Assert.Equal(Sample, ShapeBounds2D.Calculate(Rectangle2D.Builder.CreateFromMinAndSize(Sample.Min, size)));
        Assert.Equal(Sample, AxisAlignedRectangle2D.Builder.CreateFromSize(size, new(-1f, 3f)).ToRect());
        Assert.Equal(Sample, AxisAlignedRectangle2D.Builder.FromMinAndSize(Sample.Min, size).ToRect());
        Assert.True(Sample.TryGetPositiveSize(out var measured));
        Assert.Equal(size, measured);
        Assert.False(default(Rect2f).TryGetPositiveSize(out _));
        Assert.False(Rect2f.Unbounded.TryGetPositiveSize(out _));
        Assert.Throws<ArgumentException>(() => Rect2f.FromMinAndSize(Vector2f.Zero, default));
    }

    [Fact]
    public void ReportsDimensionsAndAllNineYUpAnchorPoints()
    {
        Assert.Equal(6, Sample.Width);
        Assert.Equal(10, Sample.Height);
        Assert.Equal(60, Sample.Area);
        Assert.Equal(new Vector2f(6, 10), Sample.Size);
        Assert.Equal(new Vector2f(3, 5), Sample.HalfSize);
        Assert.Equal(-4, Sample.Left);
        Assert.Equal(2, Sample.Right);
        Assert.Equal(-2, Sample.Bottom);
        Assert.Equal(8, Sample.Top);
        Assert.Equal(-1, Sample.MidX);
        Assert.Equal(3, Sample.MidY);
        Assert.Equal(new Vector2f(-4, 8), Sample.TopLeft);
        Assert.Equal(new Vector2f(-1, 8), Sample.TopCenter);
        Assert.Equal(new Vector2f(2, 8), Sample.TopRight);
        Assert.Equal(new Vector2f(-4, 3), Sample.CenterLeft);
        Assert.Equal(new Vector2f(-1, 3), Sample.Center);
        Assert.Equal(new Vector2f(2, 3), Sample.CenterRight);
        Assert.Equal(new Vector2f(-4, -2), Sample.BottomLeft);
        Assert.Equal(new Vector2f(-1, -2), Sample.BottomCenter);
        Assert.Equal(new Vector2f(2, -2), Sample.BottomRight);
    }

    [Fact]
    public void ImplementingOnlyMinAndMaxGivesConcreteTypesAndInterfacesTheSameOperations()
    {
        var value = new CustomValue(Sample.Min, Sample.Max);
        var reference = new CustomReference(Sample.Min, Sample.Max);
        IRect2f contract = value;
        Assert.Equal(6, value.Width);
        Assert.Equal(new Vector2f(-4, 8), reference.TopLeft);
        Assert.Equal(new Vector2f(-4, 3), contract.CenterLeft);
        Assert.True(value.Contains(reference));
        Assert.True(reference.Intersects(value));
        Assert.True(contract.Contains(Sample.Center));
        Assert.Equal(Sample, contract.ToRect());
    }

    [Fact]
    public void BoundsAndTheAxisAlignedShapeAdoptTheContractWhileTheOrientedShapeDoesNot()
    {
        var bounds = Rect2f.Builder.Create(Sample.Min, Sample.Max);
        var shape = Rectangle2D.Builder.Create(Sample.Min, Sample.Max);
        var axisAligned = AxisAlignedRectangle2D.Builder.Create(Sample.Min, Sample.Max);
        Assert.Equal(6, bounds.Width);
        Assert.Equal(10, shape.Height);
        Assert.Equal(new Vector2f(2, 8), axisAligned.TopRight);
        Assert.True(bounds.Intersects(axisAligned));
        Assert.True(axisAligned.Contains(bounds));
        Assert.True(axisAligned.Contains(Sample));
        Assert.Equal(bounds, new SpatialObject2D(shape).LocalBounds);
        Assert.Equal(axisAligned.Area, ((IRect2f)axisAligned).Area);
        Assert.Equal(shape.ContainsPoint(Sample.Max), axisAligned.Contains(Sample.Max));
        Assert.False(typeof(IShape2D).IsAssignableFrom(typeof(Rect2f)));
        Assert.False(typeof(IShape2D).IsAssignableFrom(typeof(IRect2f)));
        Assert.False(typeof(IRect2f).IsAssignableFrom(typeof(Rectangle2D)));
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
        Assert.Equal(expected, Sample.Contains(new Vector2f(x, y)));

    [Fact]
    public void RectangleContainmentRequiresTheWholeOtherRectangle()
    {
        Assert.True(Sample.Contains(Rect2f.Builder.Create(new(-3, -1), new(1, 7))));
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
        var first = Rect2f.Builder.Create(Vector2f.Zero, new(2));
        var second = Rect2f.Builder.Create(new(x, y), new(x + 2, y + 2));
        Assert.Equal(intersects, first.Intersects(second));
        Assert.Equal(intersects, second.Intersects(first));
        Assert.Equal(intersects, first.TryIntersect(second, out var result));
        Assert.Equal(width, result.Width);
        Assert.Equal(height, result.Height);
        if (intersects)
        {
            Assert.Equal(new Vector2f(x, y), result.Min);
            Assert.True(first.Contains(result));
            Assert.True(second.Contains(result));
        }
        else
        {
            Assert.Equal(default, result);
        }
    }

    [Fact]
    public void UnionContainsBothRectsAndClosestPointClampsToTheirEdges()
    {
        var other = new CustomReference(new(0, -5), new(10, 4));
        var union = Sample.Union(other);
        Assert.Equal(Rect2f.Builder.Create(new(-4, -5), new(10, 8)), union);
        Assert.True(union.Contains(Sample));
        Assert.True(union.Contains(other));
        Assert.Equal(new Vector2f(-4, 8), Sample.ClosestPoint(new(-10, 20)));
        Assert.Equal(new Vector2f(0, 3), Sample.ClosestPoint(new(0, 3)));
    }

    [Fact]
    public void MovementExpansionAndInsetProduceNewValuesAndClampOversizedInsets()
    {
        Assert.Equal(Rect2f.Builder.Create(new(-3, 0), new(3, 10)), Sample.TranslatedBy(new(1, 2)));
        Assert.Equal(Rect2f.Builder.Create(new(-5, -4), new(3, 10)), Sample.InflatedBy(1, 2));
        Assert.Equal(Rect2f.Builder.Create(new(-3, 0), new(1, 6)), Sample.InsetBy(1, 2));
        Assert.Equal(Rect2f.Builder.Create(new(-1, 0), new(-1, 6)), Sample.InsetBy(100, 2));
        var collapsed = Sample.InsetBy(100, 100);
        Assert.Equal(Sample.Center, collapsed.Min);
        Assert.Equal(Sample.Center, collapsed.Max);
        Assert.Equal(new Vector2f(-4, -2), Sample.Min);
        Assert.Equal(new Vector2f(2, 8), Sample.Max);
    }

    [Fact]
    public void DefaultAndZeroSizeRectsAreValidPointRects()
    {
        var point = default(Rect2f);
        Assert.Equal(0, point.Area);
        Assert.True(point.Contains(Vector2f.Zero));
        Assert.False(point.Contains(Vector2f.One));
        Assert.True(point.Intersects(point));
        Assert.Equal(Rect2f.Builder.Create(new(3, 4), new(3, 4)), Rect2f.FromSize(Vector2f.Zero, new(3, 4)));
    }

    [Fact]
    public void InfiniteBoundsKeepTheirBroadPhaseBehavior()
    {
        var unbounded = Rect2f.Unbounded;
        Assert.False(unbounded.IsFinite);
        Assert.False(unbounded.ToRect().IsFinite);
        Assert.True(unbounded.Contains(Sample));
        Assert.True(unbounded.Intersects(Sample));
        Assert.True(unbounded.TryIntersect(Sample, out var finite));
        Assert.Equal(Sample, finite);
        Assert.Equal(unbounded.ToRect(), Sample.Union(unbounded));
        Assert.Equal(unbounded, unbounded.TransformedBy(Matrix3x2.Identity));
        Assert.True(unbounded.Intersects(Rect2f.Builder.Create(Sample.Min, Sample.Max)));
    }

    [Fact]
    public void ConstructionAndInsetsRejectInvalidInputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rect2f.Builder.Create(Vector2f.One, Vector2f.Zero));
        Assert.ThrowsAny<ArgumentException>(() => Rect2f.Builder.Create(new(float.NaN, 0), Vector2f.One));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rect2f.FromSize(new Vector2f(-1, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Sample.InflatedBy(-1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => Sample.InsetBy(1, float.NaN));
        Assert.Equal(Sample, Rect2f.FromSize(new Vector2f(6, 10), new(-1, 3)));
        Assert.Equal(Sample, Rect2f.FromPoints(Sample.Max, Sample.Min));
    }

    [Fact]
    public void CustomValueRectQueriesAndCornerWritingDoNotBoxOrAllocate()
    {
        var value = new CustomValue(Sample.Min, Sample.Max);
        Span<Vector2f> corners = stackalloc Vector2f[4];
        for (var i = 0; i < 100; i++)
        {
            _ = Query(value, corners);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var total = 0f;
        for (var i = 0; i < 1000; i++)
        {
            total += Query(value, corners);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.True(total > 0);
        Assert.Equal(Sample.BottomLeft, corners[0]);
        Assert.Equal(Sample.BottomRight, corners[1]);
        Assert.Equal(Sample.TopRight, corners[2]);
        Assert.Equal(Sample.TopLeft, corners[3]);

        static float Query<T>(T rectangle, Span<Vector2f> corners) where T : IRect2f
        {
            rectangle.WriteCorners(corners);
            return rectangle.Contains(rectangle.Center) && rectangle.TryIntersect(rectangle, out var overlap)
                ? overlap.Area + rectangle.Width + rectangle.TopLeft.Y : 0;
        }
    }

    [Fact]
    public void OverlapCenterIsTheSharedRectanglesCenterOrTheGapsMiddle()
    {
        var a = Rect2f.Builder.Create(new(0, 0), new(4, 4));
        Assert.Equal(new Vector2f(3, 2), a.OverlapCenter(Rect2f.Builder.Create(new(2, -1), new(6, 5))));
        Assert.Equal(new Vector2f(5, 2), a.OverlapCenter(Rect2f.Builder.Create(new(6, 0), new(8, 4))));
    }

    private readonly record struct CustomValue(Vector2f Min, Vector2f Max) : IRect2f;
    private sealed record CustomReference(Vector2f Min, Vector2f Max) : IRect2f;
}
