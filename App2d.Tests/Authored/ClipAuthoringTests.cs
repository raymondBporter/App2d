using System.Numerics;
using App2d.Core.Characters;

namespace App2d.Tests.Authored;

public sealed class ClipAuthoringTests
{
    private static readonly ResolvedModel Model = ResolvedModel.From(TestModels.Creature());

    private static Vector3 Posed(MotionClip clip, string control, float time, ResolvedModel? model = null)
    {
        clip.Validate(model ?? Model); return PoseEvaluator.Sample(model ?? Model, clip, time).World(control);
    }

    [Theory, InlineData("body"), InlineData("shoulder"), InlineData("hand"), InlineData("foot")]
    public void PosingAControlPutsItWhereItWasDragged(string control)
    {
        var clip = TestModels.Clip(Model);
        clip.Tracks.Add(new() { Kind = MotionClip.RotateKind, Target = "body", Keys = [new() { Angle = .3f }] });
        var target = PoseEvaluator.Sample(Model, clip, .25).World(control) + new Vector3(.05f, .04f, .1f);
        var channel = ClipAuthoring.Pose(Model, clip, PoseEvaluator.Sample(Model, clip, .25), .25f, control, target);
        Assert.NotNull(channel);
        TestModels.Near(target, Posed(clip, control, .25f), 1e-4f, control);
    }

    [Fact]
    public void AnIkJointCannotBePosedDirectly() =>
        Assert.Throws<InvalidOperationException>(() => ClipAuthoring.Pose(Model, TestModels.Clip(Model), PoseEvaluator.Rest(Model), 0, "knee", Vector3.Zero));

    [Fact]
    public void AKeyAuthoredOnOneBuildIsStoredInReferenceUnits()
    {
        var longLegs = ResolvedModel.From(TestModels.Creature(), new ModelVariant
        {
            Id = "long", Name = "Long", Base = "creature", Rest = { ["body"] = new(0, 1.5f), ["hip"] = new(0, 1.5f), ["knee"] = new(.15f, .75f), ["shoulder"] = new(0, 2), ["elbow"] = new(.4f, 1.8f), ["hand"] = new(.8f, 2) },
        });
        var clip = TestModels.Clip(Model); var ratio = longLegs.Measure("leg") / Model.Measure("leg");
        ClipAuthoring.Pose(longLegs, clip, PoseEvaluator.Sample(longLegs, clip, 0), 0, "body", longLegs.Rest["body"] + new Vector3(0, .15f, 0));
        TestModels.Near(new(0, .15f / ratio, 0), ClipAuthoring.Value(clip, new(MotionClip.TranslateKind, "body"), 0).Value);
        TestModels.Near(Model.Rest["body"] + new Vector3(0, .15f / ratio, 0), Posed(clip, "body", 0));
    }

    [Fact]
    public void DraggingAPlantedEndMovesItsContactTargetNotAKey()
    {
        var clip = TestModels.Clip(Model); clip.Travel = new() { Scale = "leg", Keys = [new() { Time = 0, X = 0 }, new() { Time = 1, X = .5f }] };
        var contact = ClipAuthoring.Plant(Model, clip, PoseEvaluator.Sample(Model, clip, .2), "leg", .2f, .4f);
        Assert.Equal(.6f, contact.Finish, 4);
        var target = PoseEvaluator.Sample(Model, clip, .3).World("foot") + new Vector3(.1f, 0, 0);
        Assert.Null(ClipAuthoring.Pose(Model, clip, PoseEvaluator.Sample(Model, clip, .3), .3f, "foot", target));
        Assert.Empty(clip.Tracks);
        TestModels.Near(target, Posed(clip, "foot", .5f), 1e-4f, "planted over the whole interval");
    }

    [Fact]
    public void PlantingStopsShortOfTheNextContact()
    {
        var clip = TestModels.Clip(Model); clip.Travel.Scale = "leg";
        clip.Contacts.Add(new() { Chain = "leg", Start = .5f, Finish = .8f });
        Assert.Equal(.5f, ClipAuthoring.Plant(Model, clip, PoseEvaluator.Sample(Model, clip, .3), "leg", .3f, .4f).Finish, 4);
        Assert.Throws<InvalidOperationException>(() => ClipAuthoring.Plant(Model, clip, PoseEvaluator.Sample(Model, clip, .6), "leg", .6f));
        Assert.Throws<InvalidOperationException>(() => ClipAuthoring.Plant(Model, clip, PoseEvaluator.Rest(Model), "arm", .1f));
    }

    [Fact]
    public void KeyPoseMoveCopyAndDeleteWorkAcrossChannels()
    {
        var clip = TestModels.Clip(Model);
        ClipAuthoring.SetKey(clip, new(MotionClip.TranslateKind, "body"), 0, new(0, .1f, 0));
        ClipAuthoring.SetKey(clip, new(MotionClip.TranslateKind, "body"), 1, new(0, .3f, 0));
        ClipAuthoring.SetKey(clip, new(MotionClip.TargetKind, "arm"), .5f, new(.1f, 0, 0));
        ClipAuthoring.KeyPose(clip, .25f);
        Assert.Equal([0, .25f, .5f, 1], ClipAuthoring.KeyTimes(clip));
        Assert.Equal(.15f, ClipAuthoring.Value(clip, new(MotionClip.TranslateKind, "body"), .25f).Value.Y, 4);
        ClipAuthoring.MoveKeys(clip, .25f, .75f, copy: true);
        Assert.Equal([0, .25f, .5f, .75f, 1], ClipAuthoring.KeyTimes(clip));
        ClipAuthoring.MoveKeys(clip, .5f, .6f, [new(MotionClip.TargetKind, "arm")]);
        Assert.Contains(.6f, ClipAuthoring.KeyTimes(clip, [new(MotionClip.TargetKind, "arm")]));
        ClipAuthoring.DeleteKeys(clip, .6f);
        ClipAuthoring.SetEase(clip, 0, ClipEase.Step);
        Assert.Equal(.1f, ClipAuthoring.Value(clip, new(MotionClip.TranslateKind, "body"), .2f).Value.Y, 4);
        clip.Validate(Model);
    }

    [Fact]
    public void RetimingScalesKeysContactsMarkersAndFaces()
    {
        var clip = TestModels.Clip(Model); clip.Travel.Scale = "leg";
        ClipAuthoring.SetKey(clip, new(MotionClip.TranslateKind, "body"), .5f, new(0, .1f, 0));
        clip.Contacts.Add(new() { Chain = "leg", Start = .2f, Finish = .6f });
        ClipAuthoring.SetMarker(clip, "step", .4f);
        ClipAuthoring.Retime(clip, 2);
        Assert.Equal(1, clip.Tracks[0].Keys[0].Time, 4);
        Assert.Equal((.4f, 1.2f), (clip.Contacts[0].Start, clip.Contacts[0].Finish));
        Assert.Equal(.8f, clip.Markers[0].Time, 4);
        clip.Validate(Model);
    }
}
