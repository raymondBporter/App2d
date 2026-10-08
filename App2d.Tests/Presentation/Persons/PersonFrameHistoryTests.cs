using App2d.Core.Characters.Authored;
using App2d.Presentation.Persons;

namespace App2d.Tests.Presentation.Persons;

public sealed class PersonFrameHistoryTests
{
    [Fact]
    public void AuthoritativeFramesReplaceExtrapolatedHistory()
    {
        var clip = new MotionClip { Id = "attack" };
        var history = new PersonFrameHistory2D();
        history.Record(0, new("attack", clip, 0, false, null, PersonGear.Sword));
        history.Record(.1, new("attack", clip, .1, false, null, PersonGear.Sword));
        history.Record(.05, new("attack", clip, .03, false, null, PersonGear.Sword));
        Assert.Equal(.015, history.Sample(.025).Seconds, 5);
        Assert.Equal(.03, history.Sample(.1).Seconds, 5);
        history.Clear();
        history.Record(0, new("attack", clip, .2, false, null, PersonGear.Sword));
        Assert.Equal(.2, history.Sample(0).Seconds, 5);
    }

    [Fact]
    public void DelayedFramesKeepTheirClipAndPropMarkersAcrossRecovery()
    {
        var attack = new MotionClip { Id = "attack" };
        var recovery = new MotionClip { Id = "recovery" };
        var history = new PersonFrameHistory2D();
        history.Record(0, new("attack", attack, .05, false, null, PersonGear.Sword));
        history.Record(.1, new("attack", attack, .15, false, null, PersonGear.Sword));
        history.Record(.11, new("recovery", recovery, 0, false, null, PersonGear.Sword));
        Assert.Equal(.10, history.Sample(.05).Seconds, 5);
        Assert.Same(attack, history.Sample(.105).PropClip);
        Assert.Same(recovery, history.Sample(.11).PropClip);
        history.Record(.12, new("walk", attack, .3, true, new(recovery, .01, PersonLoadout.SwordUpperBody), PersonGear.Sword));
        history.Record(.14, new("walk", attack, .4, true, new(recovery, .03, PersonLoadout.SwordUpperBody), PersonGear.Sword));
        Assert.Equal(.02, history.Sample(.13).PropSeconds, 5);
        Assert.Same(recovery, history.Sample(.13).PropClip);
    }
}
