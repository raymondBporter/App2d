using App2d.Core.Mathematics;
using System.Numerics;

namespace App2d.Tests.Mathematics;

public sealed class Polar2DTests
{
    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(90, 0, 1)]
    [InlineData(180, -1, 0)]
    [InlineData(-90, 0, -1)]
    [InlineData(450, 0, 1)]
    public void DirectionsFollowYUpCounterClockwiseConvention(float degrees, float x, float y)
    {
        var radians = degrees * MathF.PI / 180f;
        AssertClose(new(x, y), Polar2D.Direction(radians));
        AssertClose(new Vector2(x, y) * 7f, Polar2D.ToCartesian(7, radians));
    }

    [Theory]
    [InlineData(3, 4)]
    [InlineData(-3, 4)]
    [InlineData(-3, -4)]
    [InlineData(3, -4)]
    [InlineData(3e20f, -4e20f)]
    [InlineData(-3e-30f, 4e-30f)]
    [InlineData(float.MaxValue, 0)]
    public void CartesianCoordinatesRoundTripAcrossQuadrantsAndScales(float x, float y)
    {
        var value = new Vector2(x, y);
        var polar = value.ToPolar();
        Assert.InRange(polar.AngleRadians, -MathF.PI, MathF.PI);
        Assert.Equal(value.AngleRadians, polar.AngleRadians);
        AssertClose(value, polar.ToCartesian());
        Assert.Equal(polar, Polar2D.FromCartesian(value));
    }

    [Fact]
    public void RadiusUsesVectorMagnitudeAndStoredAnglesKeepFullTurns()
    {
        var (radius, angle) = new Vector2(3, 4).ToPolar();
        Assert.Equal(5f, radius);
        Assert.InRange(angle, 0f, MathF.PI / 2);

        const float unwrapped = -3 * MathF.Tau + .37f;
        var polar = new Polar2D(2, unwrapped);
        Assert.Equal(unwrapped, polar.AngleRadians);
        AssertClose(Rotation2D.Apply(Vector2.UnitX * 2, unwrapped), polar.ToCartesian());
    }

    [Fact]
    public void ZeroHasAnExplicitHeadingWithoutDiscardingTinyDirections()
    {
        Assert.Equal(default, Vector2.Zero.ToPolar());
        Assert.Equal(Vector2.Zero, default(Polar2D).ToCartesian());
        Assert.Equal(Vector2.Zero, new Polar2D(0, 2).ToCartesian());
        Assert.Equal(0, BitConverter.SingleToInt32Bits(Polar2D.AngleOf(new(-0f, -0f))));
        Assert.Equal(MathF.PI / 2, new Vector2(0, float.Epsilon).AngleRadians);
        Assert.Equal(-MathF.PI / 2, new Vector2(0, -float.Epsilon).AngleRadians);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void NonFiniteCoordinatesAreRejected(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Polar2D(value, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Polar2D(1, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => Polar2D.Direction(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => Polar2D.AngleOf(new(value, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Polar2D.FromCartesian(new(1, value)));
    }

    [Fact]
    public void RadiusCannotBeNegativeOrOverflowWhenConverting()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Polar2D(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Polar2D.ToCartesian(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Polar2D.FromCartesian(new(float.MaxValue)));
    }

    [Fact]
    public void RepeatedConversionsDoNotAllocate()
    {
        for (var i = 0; i < 100; i++) Convert(i);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0f;
        for (var i = 0; i < 1000; i++) sum += Convert(i);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(float.IsFinite(sum));
        Assert.Equal(0, allocated);

        static float Convert(int i)
        {
            var point = Polar2D.ToCartesian(5, i * .01f);
            var polar = point.ToPolar();
            return polar.Radius + point.AngleRadians + polar.ToCartesian().X;
        }
    }

    private static void AssertClose(Vector2 expected, Vector2 actual)
    {
        var scale = Math.Max(Math.Abs(expected.X), Math.Abs(expected.Y));
        Assert.InRange(Math.Abs(actual.X / scale - expected.X / scale), 0, 2e-6f);
        Assert.InRange(Math.Abs(actual.Y / scale - expected.Y / scale), 0, 2e-6f);
    }
}
