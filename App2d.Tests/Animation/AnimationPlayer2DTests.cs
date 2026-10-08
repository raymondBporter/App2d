using App2d.Core.Animation;
using App2d.Core.Timing;

namespace App2d.Tests.Animation;

public sealed class AnimationPlayer2DTests
{
    [Fact]
    public void UsesEachStoredFrameDurationIncludingFinalLoopSample()
    {
        var clip = new AnimationClip2D<string>(
            ["first", "second", "final"],
            [0.1f, 0.25f, 0.4f],
            isLooping: true);
        var player = new AnimationPlayer2D<string>();
        player.Play(clip);

        Assert.Equal(0.75f, clip.Duration, 5);
        player.Update(0.099f);
        Assert.Equal("first", player.CurrentFrame);
        player.Update(0.002f);
        Assert.Equal("second", player.CurrentFrame);
        player.Update(0.25f);
        Assert.Equal("final", player.CurrentFrame);
        player.Update(0.398f);
        Assert.Equal("final", player.CurrentFrame);
        player.Update(0.002f);
        Assert.Equal("first", player.CurrentFrame);
    }

    [Fact]
    public void RejectsNonpositiveSampleDurationWithItsIndex()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new AnimationClip2D<int>([1, 2], [0.1f, 0f]));
        Assert.Contains("index 1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PingPongSamplesFramesInReverseAfterTheFarEnd()
    {
        var clip = new AnimationClip2D<string>(["first", "middle", "last"], [.2f, .2f, .2f]);
        var player = new AnimationPlayer2D<string>();
        player.Play(clip, endMode: PlaybackEndMode.PingPong);
        player.Update(.6f);
        Assert.Equal("last", player.CurrentFrame);
        Assert.True(player.IsPlaying);
        player.Update(.25f);
        Assert.Equal("middle", player.CurrentFrame);
        player.Update(.3f);
        Assert.Equal("first", player.CurrentFrame);
    }

    [Fact]
    public void OneShotHoldsItsFinalFrameUntilRestarted()
    {
        var clip = new AnimationClip2D<string>(["first", "last"], [.1f, .2f], isLooping: false);
        var player = new AnimationPlayer2D<string>();
        player.Play(clip); player.Update(1);
        Assert.True(player.IsFinished);
        Assert.False(player.IsPlaying);
        Assert.Equal("last", player.CurrentFrame);
        player.Resume(); Assert.False(player.IsPlaying);
        player.Play(clip);
        Assert.Equal("first", player.CurrentFrame);
        Assert.True(player.IsPlaying);
    }
}
