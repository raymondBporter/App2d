using App2d.Core.Animation;
using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Progress;

namespace App2d.Tests.Animation;

public sealed class KeyframeSegmentSamplerTests
{
    [Fact]
    public void ProgressIsEvaluatedOnceForAllScalarChannels()
    {
        var map = new CountingMap();
        var sample = new KeyframeSegmentSampler(.5f, map);
        Assert.Equal(.25f, sample.Interpolate(0f, 1f));
        Assert.Equal(3f, sample.Interpolate(2f, 6f));
        Assert.Equal(-2f, sample.Interpolate(-3f, 1f));
        Assert.Equal(6f, sample.Interpolate(8f, 0f));
        Assert.Equal(1, map.Samples);
    }

    [Fact]
    public void SamplerAcceptsExistingEasingsWithoutAddingSavedClipModes()
    {
        for (var i = 0; i <= 100; i++)
        {
            var phase = i / 100f;
            var sample = new KeyframeSegmentSampler(phase, Easing.EaseInOutSine);
            Assert.Equal((float)Easing.EaseInOutSine.Sample(phase), sample.Interpolate(0f, 1f));
        }
    }

    [Fact]
    public void CustomCurvesReplaceNamedEasing()
    {
        var curve = CubicBezier2D.Builder.Create(new(0f, 0f), new(1f / 3, 0f), new(2f / 3, 0f), new(1f, 1f));
        var sample = new KeyframeSegmentSampler(.5f, Easing.Smoother);
        Assert.Equal(.5f, sample.Interpolate(0f, 1f));
        Assert.Equal(.125f, sample.Interpolate(0f, 1f, curve), 6);
    }

    [Theory]
    [InlineData(-.5f, 2f)]
    [InlineData(0f, 2f)]
    [InlineData(1f, 8f)]
    [InlineData(1.5f, 8f)]
    public void CustomCurvesKeepExactEndpointHolds(float phase, float expected)
    {
        var curve = CubicBezier2D.Builder.Create(new(0f, 2f), new(.2f, -4f), new(.8f, 16f), new(1f, 8f));
        var sample = new KeyframeSegmentSampler(phase, Easing.Linear);
        Assert.Equal(expected, sample.Interpolate(2f, 8f, curve));
    }

    [Fact]
    public void TimingCacheKeepsPerChannelValuesAndFallsBackBeyondFourEntries()
    {
        var sample = new KeyframeSegmentSampler(.37f, Easing.Smooth);
        for (var pass = 0; pass < 2; pass++)
        {
            for (var i = 0; i < 6; i++)
            {
                var curve = CubicBezier2D.Builder.Create(new(0f, 3f), new(.1f + i * .1f, 12f + pass),
                    new(.9f - i * .05f, -4f - pass), new(1f, 3f));
                var expected = curve.Evaluate(curve.ParameterAtX(.37f)).Y;
                Assert.Equal(expected, sample.Interpolate(3f, 3f, curve));
            }
        }
    }

    private sealed class CountingMap : IProgressMap
    {
        public int Samples { get; private set; }
        public double Sample(double phase)
        {
            Samples++;
            return phase * phase;
        }
        public double Derivative(double phase) => throw new InvalidOperationException("Sampling a value needs no derivative.");
    }
}
