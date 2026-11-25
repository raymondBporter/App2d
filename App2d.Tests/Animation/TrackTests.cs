using App2d.Core.Animation;
using App2d.Core.Mathematics.Progress;

namespace App2d.Tests.Animation;

public sealed class TrackTests
{
    [Fact]
    public void HoldsTheFirstAndLastValuesOutsideTheKeys()
    {
        var track = Track.Linear(new(1, 10), new(3, 30));

        Assert.Equal(10, track.Sample(0));
        Assert.Equal(10, track.Sample(1));
        Assert.Equal(30, track.Sample(3));
        Assert.Equal(30, track.Sample(99));
    }

    [Fact]
    public void InterpolatesBetweenNeighboursAndReturnsKeysExactly()
    {
        var track = Track.Linear(new(0, 0), new(1, 10), new(3, 30), new(4, -2));

        Assert.Equal(5, track.Sample(.5f), 5);
        Assert.Equal(10, track.Sample(1));
        Assert.Equal(20, track.Sample(2), 5);
        Assert.Equal(30, track.Sample(3));
        Assert.Equal(14, track.Sample(3.5f), 5);
    }

    [Fact]
    public void AKeyCanSayHowToReachTheNextOne()
    {
        var eased = Interpolation.Linear<float>().WithProgress(Easingf.Smooth);
        var track = Track.Linear(new(0, 0, Interpolation.Hold<float, float>()), new(1, 10, eased), new(2, 20));

        Assert.Equal(0, track.Sample(.9f));
        Assert.Equal(10, track.Sample(1));
        Assert.Equal(10 + 10 * Easingf.Smooth.Sample(.25f), track.Sample(1.25f), 5);
        Assert.Equal(15, track.Sample(1.5f), 5);
    }

    [Fact]
    public void VectorsUseTheirOwnRuleAndHeldTracksStep()
    {
        var moved = Track.Of<Vector2f>(new(0, new(0, 0)), new(2, new(4, -2)));
        var held = Track.Held<int>(new(0, 1), new(1, -1));

        Assert.Equal(new Vector2f(1, -.5f), moved.Sample(.5f));
        Assert.Equal(1, held.Sample(.99f));
        Assert.Equal(-1, held.Sample(1));
        Assert.Equal(7, Track.Constant(7).Sample(123));
    }

    [Fact]
    public void KeysMustExistAndIncreaseInTime()
    {
        Assert.ThrowsAny<ArgumentException>(() => Track.Linear());
        Assert.ThrowsAny<ArgumentException>(() => Track.Linear(new(1, 0), new(1, 5)));
        Assert.ThrowsAny<ArgumentException>(() => Track.Linear(new(2, 0), new(1, 5)));
    }
}
