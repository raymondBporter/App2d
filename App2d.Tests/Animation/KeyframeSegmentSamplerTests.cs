using App2d.Core.Animation;
using App2d.Core.Mathematics.Progress;

namespace App2d.Tests.Animation;

public sealed class KeyframeSegmentSamplerTests
{
    [Fact]
    public void ProgressIsEvaluatedOnceForAllScalarChannels()
    {
        var samples = 0;
        var map = new ProgressMapf(phase => { samples++; return phase * phase; },
            _ => throw new InvalidOperationException("Sampling a value needs no derivative."));
        var sample = new KeyframeSegmentSampler(.5f, map);
        Assert.Equal(.25f, sample.Interpolate(0f, 1f));
        Assert.Equal(3f, sample.Interpolate(2f, 6f));
        Assert.Equal(-2f, sample.Interpolate(-3f, 1f));
        Assert.Equal(6f, sample.Interpolate(8f, 0f));
        Assert.Equal(new Vector3f(2f, -1f, 4f), sample.Interpolate(Vector3f.Zero, new(8f, -4f, 16f)));
        Assert.Equal(new Vector4f(2f, -1f, 4f, 8f), sample.Interpolate(Vector4f.Zero, new(8f, -4f, 16f, 32f)));
        Assert.Equal(1, samples);
    }

    [Fact]
    public void SamplerAcceptsExistingEasingsWithoutAddingSavedClipModes()
    {
        var easing = Easingf.Sine.WithDirection(EasingDirection.InOut);
        for (var i = 0; i <= 100; i++)
        {
            var phase = i / 100f;
            var sample = new KeyframeSegmentSampler(phase, easing);
            Assert.Equal(easing.Sample(phase), sample.Interpolate(0f, 1f));
        }
    }

    [Fact]
    public void CustomCurvesReplaceNamedEasing()
    {
        var curve = new BezierParameters { FirstControlTime = 1f / 3f, SecondControlTime = 2f / 3f, SecondControlValue = 0f };
        var sample = new KeyframeSegmentSampler(.5f, Easingf.Smoother);
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
        var curve = new BezierParameters { Absolute = true, FirstControlTime = .2f, FirstControlValue = -4f, SecondControlTime = .8f, SecondControlValue = 16f };
        var sample = new KeyframeSegmentSampler(phase, Easingf.Linear);
        Assert.Equal(expected, sample.Interpolate(2f, 8f, curve));
    }

}
