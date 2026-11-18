using App2d.Core.Geometry.Primitives;

namespace App2d.Tests.Geometry;

public sealed class GenericRect2Tests
{
    [Fact]
    public void FloatBoundsAndTheGenericRectangleShareTheCoordinateContract()
    {
        var packed = new Rect2f(new(-4, -2), new(2, 8));
        var generic = new Rect2<Vector2f, float>(packed.Min, packed.Max);
        Assert.Equal(packed.Width, generic.Width);
        Assert.Equal(packed.Height, generic.Height);
        Assert.Equal(packed.Area, generic.Area);
        Assert.Equal(packed.Center, generic.Center);
        Assert.Equal(packed.Contains(packed.Center), Rect2Math<Vector2f, float>.Contains(packed, packed.Center));
        Assert.True(packed.Contains(generic));
        Assert.True(generic.Contains(packed));
    }

    [Fact]
    public void DoubleBoundsKeepCoordinatesBeyondFloatPrecision()
    {
        var bounds = new Rect2d(new(16_777_217, -4), new(16_777_220, 8));
        Assert.Equal(3, bounds.Width);
        Assert.Equal(12, bounds.Height);
        Assert.Equal(36, bounds.Area);
        Assert.Equal(new Vector2d(16_777_218.5, 2), bounds.Center);
        Assert.True(bounds.Contains(new Vector2d(16_777_218, 0)));
        Assert.False(bounds.ContainsInterior(bounds.Min));
        Assert.Equal(new Vector2d(16_777_217, 8), bounds.ClosestPoint(new(0, 20)));
        Assert.Equal(25, bounds.DistanceSquaredTo(new Vector2d(16_777_214, 12)));
        Assert.Equal(5, bounds.DistanceTo(new Vector2d(16_777_214, 12)));

        var touching = new Rect2d(new(16_777_220, 0), new(16_777_224, 10));
        Assert.True(bounds.TryIntersect(touching, out var intersection));
        Assert.Equal(0, intersection.Width);
        Assert.Equal(8, intersection.Height);
        Assert.True(bounds.Union(touching).Contains(bounds));
        Assert.True(bounds.Union(touching).Contains(touching));
    }

    [Fact]
    public void DoubleBoundsHandleUnboundedCoordinatesAndLargeFiniteMidpoints()
    {
        var unbounded = Rect2.Unbounded<Vector2d, double>();
        Assert.False(unbounded.IsFinite);
        Assert.True(unbounded.Contains(Vector2d.Zero));
        Assert.True(double.IsNaN(unbounded.Center.X));
        var largest = new Rect2d(new(double.MaxValue / 2, double.Epsilon), new(double.MaxValue, double.Epsilon));
        Assert.True(double.IsFinite(largest.Center.X));
        Assert.Equal(double.Epsilon, largest.Center.Y);
        Assert.Equal(1d, default(Rect2d).DistanceTo(new Vector2d(3e200, 4e200)) / 5e200, 14);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rect2d(new(double.NaN, 0), new(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rect2d(new(2, 0), new(1, 1)));
    }

    [Fact]
    public void SelfTypedRectangleQueriesDoNotBox()
    {
        var bounds = new Rect2d(new(-4, -2), new(2, 8));
        for (var i = 0; i < 100; i++) _ = Query(bounds);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var total = 0d;
        for (var i = 0; i < 1000; i++) total += Query(bounds);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.True(total > 0);

        static double Query<TSelf>(TSelf rectangle) where TSelf : IRect2d =>
            Rect2Math<Vector2d, double>.TryIntersect(rectangle, rectangle, out var shared)
                ? shared.Area + Rect2Math<Vector2d, double>.Center(rectangle).Y : 0;
    }

}
