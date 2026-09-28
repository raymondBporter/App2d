using App2d.Presentation.World.Presentation;
using Xunit;

namespace App2d.Presentation.Tests.Enemies;

public sealed class ReactionTimeCurveTests
{
    [Fact]
    public void InfiniteSlowdownCannotCreateAClockThatNeverRecovers()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ReactionTimeCurve2D(.5f, float.PositiveInfinity, 2f));
        Assert.Equal("slowdownSeconds", error.ParamName);
    }

    [Theory]
    [InlineData(.55f, .045f, 1.6f)]
    [InlineData(.2f, .08f, 1.8f)]
    [InlineData(0f, .11f, 2f)]
    [InlineData(0f, .125f, 2f)]
    [InlineData(0f, .025f, 2.5f, .07f)]
    [InlineData(0f, .025f, 3.5f, .14f)]
    public void SmoothClockNeverReversesOrOvertakesAndFullyRepaysItsDelay(float slow, float seconds, float peak, float hold = 0)
    {
        var curve = new ReactionTimeCurve2D(slow, seconds, peak, hold);
        var previous = 0.0;
        for (var i = 0; i <= 1000; i++)
        {
            var time = curve.Duration * i / 1000.0;
            var sample = curve.Sample(time);
            Assert.InRange(sample, previous - 1e-10, time);
            if (i is > 0 and < 999)
            {
                const double delta = 1e-7;
                var numericalSpeed = (curve.Sample(time + delta) - curve.Sample(time - delta)) / (2 * delta);
                Assert.InRange(Math.Abs(curve.Speed(time) - numericalSpeed), 0, .0001);
            }
            previous = sample;
        }
        Assert.Equal(slow, curve.Speed(hold > 0 ? hold / 2 : seconds / 2), 5);
        if (hold > 0) Assert.Equal(hold * slow, curve.Sample(hold), 5);
        Assert.Equal(peak, curve.Speed(hold + seconds + curve.RecoverySeconds / 2), 5);
        Assert.Equal(curve.Duration, curve.Sample(curve.Duration));
        Assert.Equal(1, curve.Speed(curve.Duration));
        Assert.Equal(1, curve.Sample(1));
    }
}
