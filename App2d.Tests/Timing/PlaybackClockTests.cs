using App2d.Core.Timing;

namespace App2d.Tests.Timing;

public sealed class PlaybackClockTests
{
    [Fact]
    public void EndlessClockCanScalePauseResumeAndStop()
    {
        var clock = new PlaybackClock { Speed = 1.5 };
        clock.Play(); clock.Advance(2);
        Assert.Equal(3, clock.TimeSeconds);
        clock.Pause(); clock.Advance(10);
        Assert.Equal(3, clock.TimeSeconds);
        clock.Play(); clock.Advance(1);
        Assert.Equal(4.5, clock.TimeSeconds);
        clock.Stop(resetToStart: false);
        Assert.False(clock.IsPlaying);
        clock.Play(); clock.Advance(1);
        Assert.Equal(6, clock.TimeSeconds);
        clock.Stop();
        Assert.Equal(0, clock.TimeSeconds);
    }

    [Fact]
    public void HoldStopsOnItsExactEndAndSeekAllowsReplay()
    {
        var clock = new PlaybackClock(1);
        clock.Play(); clock.Advance(2);
        Assert.Equal(1, clock.TimeSeconds);
        Assert.True(clock.IsComplete);
        Assert.False(clock.IsPlaying);
        clock.Play(); Assert.False(clock.IsPlaying);
        clock.Seek(.25); clock.Play(); clock.Advance(.5);
        Assert.Equal(.75, clock.TimeSeconds, 8);
        Assert.False(clock.IsComplete);
    }

    [Fact]
    public void LoopCountsSkippedCyclesAndCanShowTheExactEndWhenScrubbed()
    {
        var clock = new PlaybackClock(1, PlaybackEndMode.Loop);
        clock.Play(); clock.Advance(3.25);
        Assert.Equal(.25, clock.TimeSeconds, 8);
        Assert.Equal(3, clock.Cycles);
        Assert.Equal(3.25, clock.ElapsedSeconds, 8);
        clock.Seek(1);
        Assert.Equal(1, clock.TimeSeconds);
        Assert.Equal(0, clock.Cycles);
        clock.Advance(.25);
        Assert.Equal(.25, clock.TimeSeconds, 8);
        Assert.Equal(1, clock.Cycles);
    }

    [Fact]
    public void PingPongUsesDurationForOneTripAndHandlesLargeSteps()
    {
        var clock = new PlaybackClock(2, PlaybackEndMode.PingPong);
        clock.Play(); clock.Advance(2);
        Assert.Equal(2, clock.TimeSeconds);
        Assert.Equal(-1, clock.Direction);
        clock.Advance(.5);
        Assert.Equal(1.5, clock.TimeSeconds, 8);
        clock.Advance(1.5);
        Assert.Equal(0, clock.TimeSeconds);
        Assert.Equal(1, clock.Cycles);
        Assert.Equal(1, clock.Direction);
        clock.Advance(4.5);
        Assert.Equal(.5, clock.TimeSeconds, 8);
        Assert.Equal(2, clock.Cycles);
    }

    [Fact]
    public void RejectsInvalidDurationsModesSpeedsAndDeltas()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlaybackClock(0));
        Assert.Throws<ArgumentException>(() => new PlaybackClock(endMode: PlaybackEndMode.Loop));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlaybackClock(1, (PlaybackEndMode)123));
        var clock = new PlaybackClock();
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Speed = double.NaN);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Seek(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(double.PositiveInfinity));
    }
}
