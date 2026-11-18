namespace App2d.Tests.Mathematics;

public sealed class PolarTests
{
    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(90, 0, 1)]
    [InlineData(180, -1, 0)]
    [InlineData(-90, 0, -1)]
    [InlineData(450, 0, 1)]
    public void DirectionsFollowYUpCounterClockwiseConvention(float degrees, float x, float y)
    {
        var radians = Angles.DegreesToRadians(degrees);
        AssertClose(new(x, y), Vector2f.Direction(radians));
        AssertClose(new Vector2f(x, y) * 7f, Vector2f.FromPolar(7, radians));
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
        var value = new Vector2f(x, y);
        var polar = value.ToPolar();
        Assert.InRange(polar.AngleRadians, -MathF.PI, MathF.PI);
        Assert.Equal(value.AngleRadians, polar.AngleRadians);
        AssertClose(value, polar.ToCartesian().ToVector2f());
        Assert.Equal(polar, Polar<float>.FromCartesian(value));
    }

    [Fact]
    public void RadiusUsesVectorMagnitudeAndStoredAnglesKeepFullTurns()
    {
        var (radius, angle) = new Vector2f(3, 4).ToPolar();
        Assert.Equal(5f, radius);
        Assert.InRange(angle, 0f, MathF.PI / 2);

        const float unwrapped = -3 * MathF.Tau + .37f;
        var polar = new Polar<float>(2, unwrapped);
        Assert.Equal(unwrapped, polar.AngleRadians);
        AssertClose((Vector2f.UnitX * 2).Rotate(unwrapped), polar.ToCartesian().ToVector2f());
    }

    [Fact]
    public void ZeroHasAnExplicitHeadingWithoutDiscardingTinyDirections()
    {
        Assert.Equal(default, Vector2f.Zero.ToPolar());
        Assert.Equal(Vector2f.Zero, default(Polar<float>).ToCartesian());
        Assert.Equal(Vector2f.Zero, new Polar<float>(0, 2).ToCartesian());
        Assert.Equal(0, BitConverter.SingleToInt32Bits(Polar<float>.AngleOf(new Vector2f(-0f, -0f))));
        Assert.Equal(-MathF.PI, new Vector2f(-0f, -0f).AngleRadians);
        Assert.Equal(Math.PI, new Vector2d(-0d, 0d).AngleRadians);
        Assert.Equal(MathF.PI / 2, new Vector2f(0, float.Epsilon).AngleRadians);
        Assert.Equal(-MathF.PI / 2, new Vector2f(0, -float.Epsilon).AngleRadians);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void NonFiniteCoordinatesAreRejected(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Polar<float>(value, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Polar<float>(1, value));
        Assert.Throws<ArgumentOutOfRangeException>(() => { Vector2f.FromAngle(value); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { Vector2d.Direction(value); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { Vector2f.FromPolar(1, value); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { Vector2f.FromPolar(value, 0); });
        Assert.Throws<ArgumentOutOfRangeException>(() => Polar<float>.AngleOf(new Vector2f(value, 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Polar<float>.FromCartesian(new Vector2f(1, value)));
    }

    [Fact]
    public void RadiusCannotBeNegativeOrOverflowWhenConverting()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Polar<float>(-1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => { Vector2f.FromPolar(-1, 0); });
        Assert.Throws<ArgumentOutOfRangeException>(() => Polar<float>.FromCartesian(new Vector2f(float.MaxValue)));
    }

    [Fact]
    public void PolarConversionsKeepTheVectorPrecision()
    {
        var wide = new Vector2d(3e200, 4e200);
        Polar<double> polar = wide.ToPolar();
        Assert.Equal(1d, polar.Radius / 5e200, 14);
        Assert.Equal(1d, polar.ToCartesian().X / wide.X, 14);
        Assert.Equal(1d, polar.ToCartesian().Y / wide.Y, 14);
        Assert.Equal(polar.ToCartesian(), Vector2d.FromPolar(polar.Radius, polar.AngleRadians));
        Assert.Equal(Vector2d.FromAngle(Math.PI / 2), Vector2d.FromAngleDegrees(90));

        var half = new TestVector2<Half>((Half)3, Half.Zero);
        Polar<Half> halfPolar = Vector2Math<TestVector2<Half>, Half>.ToPolar(half);
        Assert.Equal(((Half)3, Half.Zero), (halfPolar.Radius, halfPolar.AngleRadians));
        Assert.Equal(half, halfPolar.ToCartesian<TestVector2<Half>>());
    }

    [Fact]
    public void RepeatedConversionsDoNotAllocate()
    {
        for (var i = 0; i < 100; i++)
        {
            Convert(i);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0f;
        for (var i = 0; i < 1000; i++)
        {
            sum += Convert(i);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(float.IsFinite(sum));
        Assert.Equal(0, allocated);

        static float Convert(int i)
        {
            var point = Vector2f.FromPolar(5, i * .01f);
            var polar = point.ToPolar();
            return polar.Radius + point.AngleRadians + polar.ToCartesian().X;
        }
    }

    private static void AssertClose(Vector2f expected, Vector2f actual)
    {
        var scale = Math.Max(Math.Abs(expected.X), Math.Abs(expected.Y));
        Assert.InRange(Math.Abs(actual.X / scale - expected.X / scale), 0, 2e-6f);
        Assert.InRange(Math.Abs(actual.Y / scale - expected.Y / scale), 0, 2e-6f);
    }
}
