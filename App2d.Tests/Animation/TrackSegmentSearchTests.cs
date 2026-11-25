using App2d.Core.Animation;

namespace App2d.Tests.Animation;

public sealed class TrackSegmentSearchTests
{
    [Fact]
    public void AConstantTrackHasItsValueAtAnyTime()
    {
        var track = Track.Constant(5f);

        Assert.Equal(5f, track.Sample(-1f));
        Assert.Equal(5f, track.Sample(99f));
        Assert.Equal(5f, track.Sample(float.NaN));
    }

    [Fact]
    public void SamplingOnAndOutsideKeysReturnsTheKeysOwnValues()
    {
        var track = Track.Linear(new Key<float>(0f, 10f), new Key<float>(1f, 20f), new Key<float>(3f, 40f));

        Assert.Equal(10f, track.Sample(-5f));
        Assert.Equal(10f, track.Sample(0f));
        Assert.Equal(15f, track.Sample(.5f));
        Assert.Equal(20f, track.Sample(1f));
        Assert.Equal(30f, track.Sample(2f));
        Assert.Equal(40f, track.Sample(3f));
        Assert.Equal(40f, track.Sample(9f));
        Assert.True(float.IsNaN(track.Sample(float.NaN)));
    }
}
