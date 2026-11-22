namespace App2d.Tests.Mathematics;

public sealed class InterpolationTests
{
    private readonly record struct Heading(float Radians) : IInterpolatable<Heading, float>
    {
        public static Heading Lerp(Heading start, Heading end, float amount) =>
            new(start.Radians + Angles.WrapSigned(end.Radians - start.Radians) * amount);
    }

    [Fact]
    public void AValueTypeSuppliesItsOwnInterpolationRuleWithoutArithmeticOperators()
    {
        var start = new Heading(7f * MathF.PI / 4f);
        var end = new Heading(MathF.PI / 4f);
        var value = start.Interpolate(end, Easingf.Smooth, .5f);
        Assert.Equal(MathF.Tau, value.Radians, 5);
        Assert.Equal(MathF.PI, start.Radians.Interpolate(end.Radians, Easingf.Smooth, .5f), 5);
    }

    [Fact]
    public void AllVectorDimensionsInterpolateInTheirNativePrecision()
    {
        Assert.Equal(new Vector2f(1.25f, -.625f), Vector2f.Zero.Interpolate(new(8f, -4f), Easingf.Smooth, .25f));
        Assert.Equal(new Vector3f(1.25f, -.625f, 2.5f), Vector3f.Zero.Interpolate(new(8f, -4f, 16f), Easingf.Smooth, .25f));
        Assert.Equal(new Vector4f(1.25f, -.625f, 2.5f, 5f), Vector4f.Zero.Interpolate(new(8f, -4f, 16f, 32f), Easingf.Smooth, .25f));
        Assert.Equal(new Vector2d(1.25d, -.625d), Vector2d.Zero.Interpolate(new(8d, -4d), Easingd.Smooth, .25d));
        Assert.Equal(new Vector3d(1.25d, -.625d, 2.5d), Vector3d.Zero.Interpolate(new(8d, -4d, 16d), Easingd.Smooth, .25d));
        Assert.Equal(new Vector4d(1.25d, -.625d, 2.5d, 5d), Vector4d.Zero.Interpolate(new(8d, -4d, 16d, 32d), Easingd.Smooth, .25d));
        Assert.Equal(new Vector3d(1.000000000025d, 0d, 0d),
            Interpolation.Interpolate(new Vector3d(1d, 0d, 0d), new(1.0000000001d, 0d, 0d), Easingd.Linear, .25d));
    }

    [Fact]
    public void GenericInterpolationUsesBothInheritedAndSpecializedVectorRules()
    {
        Assert.Equal(new TestVector3<double>(1.25d, -.625d, 2.5d),
            Interpolation.Interpolate(new TestVector3<double>(), new(8d, -4d, 16d), Easingd.Smooth, .25d));

        var start = new Vector3f(1e-20f, 3e18f, -9e20f);
        var end = new Vector3f(-.123f, -4e18f, 8e20f);
        Assert.Equal(Vector3f.Lerp(start, end, Easingf.Smooth.Sample(.37f)),
            start.Interpolate(end, Easingf.Smooth, .37f));
    }

    [Fact]
    public void InterpolationSamplesOnceAndAllowsExtrapolationWithoutEvaluatingDerivatives()
    {
        var samples = 0;
        var map = new ProgressMapf(time => { samples++; return time; },
            _ => throw new InvalidOperationException("Value interpolation does not need a derivative."));
        Assert.Equal(new Vector3f(10f, -5f, 2.5f),
            Vector3f.Zero.Interpolate(new(8f, -4f, 2f), map, 1.25f));
        Assert.Equal(1, samples);
    }

    [Fact]
    public void DoubleInterpolationPreservesDetailsSmallerThanFloatPrecision()
    {
        const double start = 1d;
        const double end = 1.0000000001d;
        Assert.Equal(1.000000000025d, Interpolation.Lerp(start, end, .25d));
        Assert.Equal(1.000000000025d, start.Interpolate(end, Easingd.Linear, .25d));
        Assert.Equal(.25d, Interpolation.InverseLerp(10d, 20d, 12.5d));
        Assert.Equal(0d, Interpolation.InverseLerpClamped(10d, 20d, 0d));
        var vector = Interpolation.Interpolate(new Vector2d(start), new Vector2d(end), Easingd.Linear, .25d);
        Assert.Equal(new Vector2d(1.000000000025d), vector);
    }

    [Fact]
    public void EitherProgressPrecisionCanBlendFloatOrDoubleScalars()
    {
        Assert.Equal(11.5625f, Interpolation.Interpolate(10f, 20f, Easingf.Smooth, .25f));
        Assert.Equal(11.5625d, Interpolation.Interpolate(10d, 20d, Easingf.Smooth, .25f));
        Assert.Equal(11.5625f, Interpolation.Interpolate(10f, 20f, Easingd.Smooth, .25d));
        Assert.Equal(11.5625d, Interpolation.Interpolate(10d, 20d, Easingd.Smooth, .25d));
        var overshoot = new ProgressMapf(_ => 1.2f, _ => 0f);
        Assert.True(Interpolation.Interpolate(0f, 10f, overshoot, .7f) > 10f);
        Assert.True(Interpolation.Interpolate(0d, 10d, overshoot, .7f) > 10d);
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
    public void InterpolateAppliesTheProgressMapBeforeLerp()
    {
        Assert.Equal(12.5f, Interpolation.Lerp(10f, 20f, 0.25f), 6);
        Assert.Equal(11.5625f, Interpolation.Interpolate(10f, 20f, Easingf.Smooth, 0.25f), 6);

        var vector = Vector2f.Zero.Interpolate(new Vector2f(8f, -4f), Easingf.Smoother, 0.5f);
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
