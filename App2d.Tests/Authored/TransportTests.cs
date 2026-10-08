using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Timing;

namespace App2d.Tests.Authored;

public sealed class TransportTests
{
    [Fact]
    public void LoopAccumulatesTravelWhilePlayingAndDropsCyclesWhenPaused()
    {
        var clip = new MotionClip { Duration = 1, Loop = true };
        var transport = new Transport { Speed = 2 };
        transport.Play(clip);
        transport.Advance(clip, .625f);
        Assert.Equal(.25f, transport.Time, 5);
        Assert.Equal(1, transport.Cycles);
        Assert.Equal(1.25, transport.Seconds(clip), 8);
        transport.Pause();
        Assert.Equal(0, transport.Cycles);
        Assert.Equal(.25, transport.Seconds(clip), 8);
        transport.Seek(clip, clip.Duration);
        Assert.Equal(clip.Duration, transport.Time);
    }

    [Fact]
    public void OneShotHoldsTheEndAndPingPongCanPreviewABoneClip()
    {
        var clip = new MotionClip { Duration = 1 };
        var transport = new Transport();
        transport.Play(clip); transport.Advance(clip, 2);
        Assert.False(transport.Playing);
        Assert.Equal(1, transport.Time);
        transport.Play(clip);
        Assert.Equal(0, transport.Time);

        transport.EndModeOverride = PlaybackEndMode.PingPong;
        transport.Seek(clip, 0); transport.Advance(clip, 1.25f);
        Assert.Equal(.75f, transport.Time, 5);
        Assert.Equal(.75, transport.Seconds(clip), 5);

        transport.EndModeOverride = PlaybackEndMode.Loop;
        transport.Seek(clip, 0); transport.Advance(clip, 1.25f);
        Assert.Equal(.25f, transport.Time, 5);
        Assert.Equal(.25, transport.Seconds(clip), 5);
        Assert.False(transport.Repeats(clip));
    }
}
