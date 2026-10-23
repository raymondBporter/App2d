using App2d.Core.Animation;

namespace App2d.Tests.Animation;

public sealed class AnimationClip2DTests
{
    [Fact]
    public void UniformClipCopiesFramesAndUsesRequestedFrameRate()
    {
        string[] frames = ["first", "second", "last"];
        var clip = new AnimationClip2D<string>(frames.Select(frame => frame), 4f, isLooping: false);
        frames[0] = "changed";

        Assert.Equal(3, clip.FrameCount);
        Assert.Equal("first", clip[0]);
        Assert.Equal(4f, clip.FramesPerSecond);
        Assert.Equal(.25f, clip.FrameDuration);
        Assert.Equal(.75f, clip.Duration);
        Assert.False(clip.IsLooping);
        Assert.Equal(0, clip.GetFrameIndexAtTime(0f));
        Assert.Equal(1, clip.GetFrameIndexAtTime(.25f));
        Assert.Equal(2, clip.GetFrameIndexAtTime(.75f));
    }

    [Fact]
    public void UniformClipRejectsEmptyFrames()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new AnimationClip2D<string>([], 4f));

        Assert.Equal("frames", exception.ParamName);
    }

    [Fact]
    public void UniformClipRejectsNullFrames()
    {
        var exception = Assert.Throws<ArgumentException>(() => new AnimationClip2D<string>(["first", null!], 4f));

        Assert.Equal("frames", exception.ParamName);
    }
}
