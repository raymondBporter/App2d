using App2d.Core.Geometry.Primitives;
using App2d.Tests.Mathematics;
using System.Numerics;

namespace App2d.Tests.Geometry;

public sealed class GenericRect2Tests
{
    [Fact]
    public void FromSizeSupportsScalarFirstMultiplicationForFloatDoubleAndHalf()
    {
        CheckFromSize<Vector2f, float>();
        CheckFromSize<Vector2d, double>();
        CheckFromSize<TestVector2<Half>, Half>();
    }

    [Fact]
    public void RectanglesSupportInstanceAndStaticDeconstruction()
    {
        var rectangle = Rect2d.Builder.Create(new(-4, -2), new(2, 8));
        var (min, max) = rectangle;
        Assert.Equal(rectangle.Min, min);
        Assert.Equal(rectangle.Max, max);
        Rect2d.Deconstruct(rectangle, out min, out max);
        Assert.Equal(rectangle.Min, min);
        Assert.Equal(rectangle.Max, max);
    }

    [Fact]
    public void ScalarPropertiesAnchorsAndProjectionsMatchTheBounds()
    {
        var rectangle = Rect2d.Builder.Create(new(-4, -2), new(2, 8));
        Assert.Equal(-1d, rectangle.CenterX);
        Assert.Equal(3d, rectangle.CenterY);
        Assert.Equal(new Vector2d(-1, 3), rectangle.Center);
        Assert.Equal(10d, rectangle.MaxSideLength);
        Assert.Equal(6d, rectangle.MinSideLength);
        Assert.Equal(new Vector2d(3, 5), rectangle.HalfSize);
        Assert.Equal(Math.Sqrt(136), rectangle.DiagonalLength, 14);
        Assert.Equal(new Vector2d(-4, 8), rectangle.TopLeft);
        Assert.Equal(new Vector2d(-1, 8), rectangle.TopCenter);
        Assert.Equal(new Vector2d(2, 8), rectangle.TopRight);
        Assert.Equal(new Vector2d(-4, 3), rectangle.CenterLeft);
        Assert.Equal(new Vector2d(2, 3), rectangle.CenterRight);
        Assert.Equal(new Vector2d(-4, -2), rectangle.BottomLeft);
        Assert.Equal(new Vector2d(-1, -2), rectangle.BottomCenter);
        Assert.Equal(new Vector2d(2, -2), rectangle.BottomRight);
        Assert.Equal(-4d, rectangle.ProjectionX.Min);
        Assert.Equal(2d, rectangle.ProjectionX.Max);
        Assert.Equal(-2d, rectangle.ProjectionY.Min);
        Assert.Equal(8d, rectangle.ProjectionY.Max);
    }

    [Theory]
    [InlineData(3, 4, 6, 8, 5, 10)]
    [InlineData(-6, -8, -3, -4, 5, 10)]
    [InlineData(-3, -4, 6, 8, 0, 10)]
    [InlineData(-3, 4, 6, 8, 4, 10)]
    public void OriginDistancesChooseTheClosestPointAndFarthestCorner(
        double minX, double minY, double maxX, double maxY, double nearest, double farthest)
    {
        var rectangle = Rect2d.Builder.Create(new(minX, minY), new(maxX, maxY));
        Assert.Equal(nearest, rectangle.DistanceToOrigin);
        Assert.Equal(farthest, rectangle.MaxDistanceToOrigin);
    }

    [Fact]
    public void LengthsAndHalfSizesAvoidUnnecessaryFloatingPointOverflowAndUnderflow()
    {
        var large = Rect2d.Builder.Create(Vector2d.Zero, new(3e200, 4e200));
        Assert.Equal(1d, large.DiagonalLength / 5e200, 14);
        Assert.Equal(1d, large.MaxDistanceToOrigin / 5e200, 14);
        var tiny = Rect2d.Builder.Create(Vector2d.Zero, new(3e-200, 4e-200));
        Assert.Equal(1d, tiny.DiagonalLength / 5e-200, 14);
        var largeFloat = Rect2<Vector2f, float>.Builder.Create(Vector2f.Zero, new(3e30f, 4e30f));
        Assert.Equal(1f, largeFloat.DiagonalLength / 5e30f, 6);
        var tinyFloat = Rect2<Vector2f, float>.Builder.Create(Vector2f.Zero, new(3e-30f, 4e-30f));
        Assert.Equal(1f, tinyFloat.DiagonalLength / 5e-30f, 6);

        var wide = Rect2d.Builder.Create(new(-double.MaxValue, double.Epsilon), new(double.MaxValue, 3 * double.Epsilon));
        Assert.Equal(double.PositiveInfinity, wide.Width);
        Assert.Equal(new Vector2d(double.MaxValue, double.Epsilon), wide.HalfSize);
    }

    [Theory]
    [InlineData(double.NaN, 0, 1, 1)]
    [InlineData(0, 0, 1, double.NaN)]
    [InlineData(2, 0, 1, 1)]
    [InlineData(0, 2, 1, 1)]
    [InlineData(double.PositiveInfinity, 0, double.PositiveInfinity, 1)]
    [InlineData(double.NegativeInfinity, 0, double.NegativeInfinity, 1)]
    [InlineData(0, double.PositiveInfinity, 1, double.PositiveInfinity)]
    [InlineData(0, double.NegativeInfinity, 1, double.NegativeInfinity)]
    public void ValidityValidationAndConstructionAgreeOnInvalidAxes(double minX, double minY, double maxX, double maxY)
    {
        var min = new Vector2d(minX, minY);
        var max = new Vector2d(maxX, maxY);
        Assert.False(Rect2d.IsValid(min, max));
        Assert.ThrowsAny<ArgumentException>(() => Rect2d.Validate(min, max));
        Assert.ThrowsAny<ArgumentException>(() => Rect2d.Builder.Create(min, max));
    }

    [Fact]
    public void OutwardInfiniteBoundsKeepProjectionsAndOriginDistancesDefined()
    {
        var rectangle = Rect2d.Unbounded;
        Assert.True(Rect2d.IsValid(rectangle.Min, rectangle.Max, allowUnbounded: true));
        Rect2d.Validate(rectangle.Min, rectangle.Max, allowUnbounded: true);
        Assert.True(rectangle.ProjectionX.IsEntire);
        Assert.True(rectangle.ProjectionY.IsEntire);
        Assert.Equal(0d, rectangle.DistanceToOrigin);
        Assert.Equal(double.PositiveInfinity, rectangle.MaxDistanceToOrigin);
        Assert.Equal(double.PositiveInfinity, rectangle.DiagonalLength);
        Assert.Equal(new Vector2d(double.PositiveInfinity, double.PositiveInfinity), rectangle.HalfSize);
    }

    [Fact]
    public void FromSizeAcceptsZeroAndRejectsInvalidSizesAndCenters()
    {
        var point = Rect2d.FromSize(Vector2d.Zero, new(3, 4));
        Assert.Equal(new Vector2d(3, 4), point.Min);
        Assert.Equal(point.Min, point.Max);
        Assert.Throws<ArgumentOutOfRangeException>(() => Rect2d.FromSize(new(-1, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rect2d.FromSize(new(double.NaN, 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rect2d.FromSize(new(1, double.PositiveInfinity)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rect2d.FromSize(new(1, 2), new(double.NaN, 0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rect2d.FromSize(new(1, 2), new(0, double.PositiveInfinity)));
    }

    [Fact]
    public void FloatBoundsAndTheGenericRectangleShareTheCoordinateContract()
    {
        var packed = Rect2f.Builder.Create(new(-4, -2), new(2, 8));
        var generic = Rect2<Vector2f, float>.Builder.Create(packed.Min, packed.Max);
        Assert.Equal(packed.Width, generic.Width);
        Assert.Equal(packed.Height, generic.Height);
        Assert.Equal(packed.Area, generic.Area);
        Assert.Equal(packed.Center, generic.Center);
        Assert.Equal(packed.Contains(packed.Center), generic.Contains(packed.Center));
        Assert.Equal(generic, Rect2<Vector2f, float>.From(packed));
        Assert.True(packed.Contains(generic));
        Assert.True(generic.Contains(Rect2<Vector2f, float>.From(packed)));
    }

    [Fact]
    public void DoubleBoundsKeepCoordinatesBeyondFloatPrecision()
    {
        var bounds = Rect2d.Builder.Create(new(16_777_217, -4), new(16_777_220, 8));
        Assert.Equal(3, bounds.Width);
        Assert.Equal(12, bounds.Height);
        Assert.Equal(36, bounds.Area);
        Assert.Equal(new Vector2d(16_777_218.5, 2), bounds.Center);
        Assert.True(bounds.Contains(new Vector2d(16_777_218, 0)));
        Assert.False(bounds.ContainsInterior(bounds.Min));
        Assert.Equal(new Vector2d(16_777_217, 8), bounds.ClosestPoint(new(0, 20)));
        Assert.Equal(25, bounds.DistanceSquaredTo(new Vector2d(16_777_214, 12)));
        Assert.Equal(5, bounds.DistanceTo(new Vector2d(16_777_214, 12)));

        var touching = Rect2d.Builder.Create(new(16_777_220, 0), new(16_777_224, 10));
        Assert.True(bounds.TryIntersect(touching, out var intersection));
        Assert.Equal(0, intersection.Width);
        Assert.Equal(8, intersection.Height);
        Assert.True(bounds.Union(touching).Contains(bounds));
        Assert.True(bounds.Union(touching).Contains(touching));
    }

    [Fact]
    public void DoubleBoundsHandleUnboundedCoordinatesAndLargeFiniteMidpoints()
    {
        var unbounded = Rect2d.Unbounded;
        Assert.False(unbounded.IsFinite);
        Assert.True(unbounded.Contains(Vector2d.Zero));
        Assert.True(double.IsNaN(unbounded.Center.X));
        var largest = Rect2d.Builder.Create(new(double.MaxValue / 2, double.Epsilon), new(double.MaxValue, double.Epsilon));
        Assert.True(double.IsFinite(largest.Center.X));
        Assert.Equal(double.Epsilon, largest.Center.Y);
        Assert.Equal(1d, default(Rect2d).DistanceTo(new Vector2d(3e200, 4e200)) / 5e200, 14);
        Assert.Throws<ArgumentOutOfRangeException>(() => Rect2d.Builder.Create(new(double.NaN, 0), new(1, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Rect2d.Builder.Create(new(2, 0), new(1, 1)));
    }

    [Fact]
    public void ConvertingAnyBoundsTypeAndQueryingItDoesNotBox()
    {
        var bounds = Rect2d.Builder.Create(new(-4, -2), new(2, 8));
        for (var i = 0; i < 100; i++) _ = Query(bounds);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var total = 0d;
        for (var i = 0; i < 1000; i++) total += Query(bounds);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
        Assert.True(total > 0);

        static double Query<TSelf>(TSelf bounds) where TSelf : IRect2d
        {
            var rectangle = Rect2d.From(bounds);
            return rectangle.TryIntersect(rectangle, out var shared) ? shared.Area + rectangle.Center.Y : 0;
        }
    }

    private static void CheckFromSize<TVector, TReal>()
        where TVector : unmanaged, IVector2<TVector, TReal>
        where TReal : unmanaged, IFloatingPointIeee754<TReal>
    {
        var size = TVector.Create(TReal.CreateChecked(6), TReal.CreateChecked(10));
        var center = TVector.Create(-TReal.One, TReal.CreateChecked(3));
        var rectangle = Rect2<TVector, TReal>.FromSize(size, center);
        Assert.Equal(TVector.Create(TReal.CreateChecked(-4), TReal.CreateChecked(-2)), rectangle.Min);
        Assert.Equal(TVector.Create(TReal.CreateChecked(2), TReal.CreateChecked(8)), rectangle.Max);
    }
}
