namespace App2d.Tests.Mathematics;

public sealed class InterpolationTests
{
    [Fact]
    public void DoubleInterpolationPreservesDetailsSmallerThanFloatPrecision()
    {
        const double start = 1d;
        const double end = 1.0000000001d;
        Assert.Equal(1.000000000025d, Interpolation.Lerp(start, end, .25d));
        Assert.Equal(1.000000000025d, Interpolation.Lerp(start, end, .25d, Easingd.Linear));
        Assert.Equal(.25d, Interpolation.InverseLerp(10d, 20d, 12.5d));
        Assert.Equal(0d, Interpolation.InverseLerpClamped(10d, 20d, 0d));
        var vector = Interpolation.Lerp(new Vector2d(start), new Vector2d(end), .25d, Easingd.Linear);
        Assert.Equal(new Vector2d(1.000000000025d), vector);
    }

    [Fact]
    public void EitherProgressPrecisionCanBlendFloatOrDoubleValues()
    {
        Assert.Equal(11.5625f, Interpolation.Lerp(10f, 20f, .25f, Easingf.Smooth));
        Assert.Equal(11.5625d, Interpolation.Lerp(10d, 20d, .25f, Easingf.Smooth));
        Assert.Equal(11.5625f, Interpolation.Lerp(10f, 20f, .25d, Easingd.Smooth));
        Assert.Equal(11.5625d, Interpolation.Lerp(10d, 20d, .25d, Easingd.Smooth));
        Assert.Equal(new Vector2d(1.25d, -.625d), Interpolation.Lerp(Vector2d.Zero, new Vector2d(8d, -4d), .25f, Easingf.Smooth));
        var overshoot = new ProgressMapf(_ => 1.2f, _ => 0f);
        Assert.True(Interpolation.Lerp(0f, 10f, .7f, overshoot) > 10f);
        Assert.True(Interpolation.Lerp(0d, 10d, .7f, overshoot) > 10d);
    }

    [Fact]
    public void MachineEpsilonDescribesTheGapAboveOneInEachPrecision()
    {
        Assert.Equal(MathF.ScaleB(1f, -23), Realf.MachineEpsilon);
        Assert.Equal(Math.ScaleB(1d, -52), Reald.MachineEpsilon);
        Assert.Equal(float.BitIncrement(1f), 1f + Realf.MachineEpsilon);
        Assert.Equal(double.BitIncrement(1d), 1d + Reald.MachineEpsilon);
    }

    [Fact]
    public void LerpCanComposeWithAProgressMapping()
    {
        Assert.Equal(12.5f, Interpolation.Lerp(10f, 20f, 0.25f), 6);
        Assert.Equal(11.5625f, Interpolation.Lerp(10f, 20f, 0.25f, Easingf.Smooth), 6);

        var vector = Interpolation.Lerp(Vector2f.Zero, new Vector2f(8f, -4f), 0.5f, Easingf.Smoother);
        Assert.Equal(new Vector2f(4f, -2f), vector);
    }

    [Fact]
    public void InverseLerpCanBeClampedOrUnclamped()
    {
        Assert.Equal(0.25f, Interpolation.InverseLerp(10f, 20f, 12.5f), 6);
        Assert.Equal(-1f, Interpolation.InverseLerp(10f, 20f, 0f), 6);
        Assert.Equal(0f, Interpolation.InverseLerpClamped(10f, 20f, 0f), 6);
        Assert.Equal(0f, Interpolation.InverseLerp(4f, 4f, 100f), 6);
    }
}
