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

    [Theory]
    [InlineData(10f)]
    [InlineData(24f)]
    [InlineData(60f)]
    public void UniformTimingPreservesRoundedFrameBoundaries(float framesPerSecond)
    {
        const int count = 257;
        var duration = 1f / framesPerSecond;
        var clip = new AnimationClip2D<int>(Enumerable.Range(0, count), framesPerSecond, isLooping: false);
        var explicitDurations = new AnimationClip2D<int>(Enumerable.Range(0, count), Enumerable.Repeat(duration, count));
        Assert.True(explicitDurations.HasUniformFrameDuration);
        Assert.Equal(clip.Duration, explicitDurations.Duration);
        for (var index = 1; index < count; index++)
        {
            var boundary = (float)((double)duration * index);
            foreach (var timed in new[] { clip, explicitDurations })
            {
                Assert.Equal(index - 1, timed.GetFrameIndexAtTime(float.BitDecrement(boundary)));
                Assert.Equal(index, timed.GetFrameIndexAtTime(boundary));
                Assert.Equal(index, timed.GetFrameIndexAtTime(float.BitIncrement(boundary)));
                Assert.Equal(duration, timed.GetFrameDuration(index));
            }
        }
        Assert.Equal(count - 1, clip.GetFrameIndexAtTime(clip.Duration));
        Assert.Equal(count - 1, clip.GetFrameIndexAtTime(clip.Duration + 1f));
    }

    [Fact]
    public void VariableTimingCopiesDurationsAndMovesToTheNextFrameAtExactBoundaries()
    {
        float[] durations = [.125f, .25f, .5f];
        var clip = new AnimationClip2D<int>([0, 1, 2], durations, isLooping: false);
        durations[0] = 10f;
        Assert.False(clip.HasUniformFrameDuration);
        Assert.Equal(.875f, clip.Duration);
        Assert.Equal(.125f, clip.GetFrameDuration(0));
        Assert.Equal(0, clip.GetFrameIndexAtTime(float.BitDecrement(.125f)));
        Assert.Equal(1, clip.GetFrameIndexAtTime(.125f));
        Assert.Equal(1, clip.GetFrameIndexAtTime(float.BitDecrement(.375f)));
        Assert.Equal(2, clip.GetFrameIndexAtTime(.375f));
        Assert.Equal(2, clip.Sample(10d));
    }

    [Fact]
    public void RoundedEndTimesCanCoincideWithoutLosingTheAuthoredDurations()
    {
        var clip = new AnimationClip2D<int>([0, 1, 2], [1f, 1e-9f, 2f]);
        Assert.Equal(1e-9f, clip.GetFrameDuration(1));
        Assert.Equal(0, clip.GetFrameIndexAtTime(float.BitDecrement(1f)));
        Assert.Equal(2, clip.GetFrameIndexAtTime(1f));
    }

    [Fact]
    public void FrameAccessAndQueriesValidateIndicesAndTimeInEitherTimingMode()
    {
        var uniform = new AnimationClip2D<int>([0, 1], 2f);
        var variable = new AnimationClip2D<int>([0, 1], [.25f, .5f]);
        foreach (var clip in new[] { uniform, variable })
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => clip.GetFrameDuration(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => clip.GetFrameDuration(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => clip.GetFrameIndexAtTime(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => clip.GetFrameIndexAtTime(float.NaN));
        }
    }
}
