using App2d.Core.Mathematics.Progress;
using App2d.Core.Animation;
using App2d.Core.Characters.Authored;
using System.Numerics;

namespace App2d.Tests.Authored;

public sealed class ClipAuthoringTests
{
    private static readonly ResolvedModel Model = ResolvedModel.From(TestModels.Creature());

    [Fact]
    public void NamedForwardEasingCanBeAuthoredAndOtherTravelIsRefusedBeforeMutation()
    {
        var clip = TestModels.Clip(Model);
        clip.Travel.Keys = [new() { Curve = new KeyCurve2D() }, new() { Time = 1f, Value = Vector3.UnitX }];
        ClipAuthoring.SetEase(clip, 0f, ProgressMapKind.Smoother);
        Assert.Equal(ProgressMapKind.Smoother, clip.Travel.Keys[0].Ease);
        Assert.Null(clip.Travel.Keys[0].Curve);
        Assert.Throws<InvalidDataException>(() => ClipAuthoring.SetEase(clip, 0f, ProgressMapKind.Reverse));
        Assert.Equal(ProgressMapKind.Smoother, clip.Travel.Keys[0].Ease);
    }

    [Fact]
    public void ExcerptsKeepNewPresetsOnKeyBoundariesAndRefuseAnInexactInteriorCut()
    {
        var clip = TestModels.Clip(Model);
        clip.Duration = 1f;
        clip.Tracks = [new() { Kind = MotionClip.RotateKind, Target = "body",
            Keys = [new() { Ease = ProgressMapKind.EaseInOutSine }, new() { Time = 1f, Angle = 1f }] }];
        var before = clip.ToJson();
        var whole = ClipAuthoring.Excerpt(clip, 0f, 1f, "whole", "Whole");
        Assert.Equal(ProgressMapKind.EaseInOutSine, whole.Tracks[0].Keys[0].Ease);
        foreach (var time in new[] { 0f, .1f, .5f, .9f, 1f })
            Assert.Equal(PoseEvaluator.Interpolate(clip.Tracks[0], time), PoseEvaluator.Interpolate(whole.Tracks[0], time));
        Assert.Contains("Cut at its key times", Assert.Throws<InvalidOperationException>(() => ClipAuthoring.Excerpt(clip, .2f, .8f, "part", "Part")).Message);
        Assert.Equal(before, clip.ToJson());
    }

    private static Vector3 Posed(MotionClip clip, string control, float time, ResolvedModel? model = null)
    {
        clip.Validate(model ?? Model);
        return PoseEvaluator.Sample(model ?? Model, clip, time).World(control);
    }

    [Theory, InlineData("body"), InlineData("shoulder"), InlineData("hand"), InlineData("foot")]
    public void PosingAControlPutsItWhereItWasDragged(string control)
    {
        var clip = TestModels.Clip(Model);
        clip.Tracks.Add(new()
        {
            Kind = MotionClip.RotateKind,
            Target = "body",
            Keys = [new() { Angle = .3f }]
        });
        var target = PoseEvaluator.Sample(Model, clip, .25).World(control) + new Vector3(.05f, .04f, .1f);
        var channel = ClipAuthoring.Pose(Model, clip, PoseEvaluator.Sample(Model, clip, .25), .25f, control, target);
        Assert.NotNull(channel);
        TestModels.Near(target, Posed(clip, control, .25f), 1e-4f, control);
    }

    [Fact]
    public void AnExcerptPlaysThatSectionOfTheClipFromZero()
    {
        var clip = TestModels.Clip(Model);
        clip.Tracks.Add(new()
        {
            Kind = MotionClip.RotateKind,
            Target = "body",
            Keys = [new() { Time = 0, Angle = 0 }, new() { Time = .2f, Angle = .4f, Ease = ProgressMapKind.Smooth }, new() { Time = .8f, Angle = -.2f }]
        });
        clip.Markers = [new() { Id = "early", Time = .1f }, new() { Id = "strike", Time = .5f }];
        var part = ClipAuthoring.Excerpt(clip, .2f, .8f, "part", "Part"); // cut on the body's keys
        Assert.Equal(.6f, part.Duration, 5);
        foreach (var t in new[] { 0, .1f, .3f, .6f })
        {
            TestModels.Near(Posed(clip, "hand", .2f + t), Posed(part, "hand", t), 1e-4f, $"hand at {t}");
        }

        Assert.Equal(ProgressMapKind.Smooth, part.Tracks.Single(t => t.Target == "body").Keys[0].Ease); // an end on a key keeps its easing
        Assert.Equal([("strike", .3f)], part.Markers.Select(m => (m.Id, MathF.Round(m.Time, 4))));
    }

    [Fact]
    public void AnIkJointCannotBePosedDirectly() =>
        Assert.Throws<InvalidOperationException>(() => ClipAuthoring.Pose(Model, TestModels.Clip(Model), PoseEvaluator.Rest(Model), 0, "knee", Vector3.Zero));

    [Fact]
    public void AKeyAuthoredOnOneBuildIsStoredInReferenceUnits()
    {
        var stretched = TestModels.Stretched(TestModels.Creature(), "long", "Long",
            ("body", new(0, 1.5f)), ("hip", new(0, 1.5f)), ("knee", new(.15f, .75f)), ("shoulder", new(0, 2)), ("elbow", new(.4f, 1.8f)), ("hand", new(.8f, 2)));
        var longLegs = ResolvedModel.From(TestModels.Creature(), stretched);
        var clip = TestModels.Clip(Model);
        var ratio = longLegs.Measure("leg") / Model.Measure("leg");
        ClipAuthoring.Pose(longLegs, clip, PoseEvaluator.Sample(longLegs, clip, 0), 0, "body", longLegs.Rest["body"] + new Vector3(0, .15f, 0));
        TestModels.Near(new(0, .15f / ratio, 0), ClipAuthoring.Value(clip, new(MotionClip.TranslateKind, "body"), 0).Value);
        TestModels.Near(Model.Rest["body"] + new Vector3(0, .15f / ratio, 0), Posed(clip, "body", 0));
    }

    [Fact]
    public void DraggingAPlantedEndMovesItsContactTargetNotAKey()
    {
        var clip = TestModels.Clip(Model);
        clip.Travel = new()
        {
            Scale = "leg",
            Keys = [new() { Time = 0, Value = new(0, 0, 0) }, new() { Time = 1, Value = new(.5f, 0, 0) }]
        };
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
        var clip = TestModels.Clip(Model);
        clip.Travel.Scale = "leg";
        clip.Contacts.Add(new()
        {
            Chain = "leg",
            Start = .5f,
            Finish = .8f
        });
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
        ClipAuthoring.SetEase(clip, 0, ProgressMapKind.Step);
        Assert.Equal(.1f, ClipAuthoring.Value(clip, new(MotionClip.TranslateKind, "body"), .2f).Value.Y, 4);
        clip.Validate(Model);
    }

    [Fact]
    public void MovingOntoAnotherKeyPreservesTheOutgoingCurveAndBend()
    {
        var clip = TestModels.Clip(Model);
        var curve = new KeyCurve2D { Absolute = true, Control1 = new(1f / 3, 2), Control2 = new(2f / 3, 2) };
        var channel = new Channel(MotionClip.TargetKind, "arm");
        clip.Tracks = [new() { Kind = channel.Kind, Target = channel.Target, Keys = [
            new() { Curve = curve, Bend = -1 }, new() { Time = .5f, Value = Vector3.One }, new() { Time = 1 } ] }];

        ClipAuthoring.MoveKeys(clip, 0, .5f, [channel]);

        var keys = clip.Tracks[0].Keys;
        Assert.Equal([.5f, 1f], keys.Select(k => k.Time));
        Assert.Same(curve, keys[0].Curve);
        Assert.Equal(-1, keys[0].Bend);
        Assert.Equal(1.5f, ClipAuthoring.Value(clip, channel, .75f).Value.X, 5);
        Assert.Equal(Vector3.Zero, ClipAuthoring.Value(clip, channel, 1).Value);
        clip.Validate(Model);
    }

    [Fact]
    public void NearbyKeyEditsReplaceTheValueAndKeepItsCurve()
    {
        var clip = TestModels.Clip(Model);
        var channel = new Channel(MotionClip.TranslateKind, "body");
        var curve = new KeyCurve2D { Absolute = true, Control1 = new(.2f, 2), Control2 = new(.8f, -1) };
        ClipAuthoring.SetKey(clip, channel, .5f, Vector3.Zero);
        ClipAuthoring.SetCurve(clip, channel, .5f, 0, curve);
        ClipAuthoring.SetKey(clip, channel, .5f + ClipAuthoring.SameTime / 2, Vector3.UnitX);

        var key = Assert.Single(clip.Tracks[0].Keys);
        Assert.Equal(Vector3.UnitX, key.Value);
        Assert.Equal(curve, key.Curve);
        ClipAuthoring.DeleteKeys(clip, .5f);
        Assert.Empty(clip.Tracks);
    }

    [Fact]
    public void KeyTimesUseTheSameToleranceAsKeyEditing()
    {
        var clip = TestModels.Clip(Model);
        var channel = new Channel(MotionClip.TranslateKind, "body");
        ClipAuthoring.SetKey(clip, channel, ClipAuthoring.SameTime, Vector3.UnitX);
        ClipAuthoring.SetKey(clip, channel, 0, Vector3.Zero);
        Assert.Equal([0, ClipAuthoring.SameTime], ClipAuthoring.KeyTimes(clip));
    }

    [Fact]
    public void RetimingScalesKeysContactsMarkersAndFaces()
    {
        var clip = TestModels.Clip(Model);
        clip.Travel.Scale = "leg";
        ClipAuthoring.SetKey(clip, new(MotionClip.TranslateKind, "body"), .5f, new(0, .1f, 0));
        clip.Contacts.Add(new()
        {
            Chain = "leg",
            Start = .2f,
            Finish = .6f
        });
        ClipAuthoring.SetMarker(clip, "step", .4f);
        ClipAuthoring.Retime(clip, 2);
        Assert.Equal(1, clip.Tracks[0].Keys[0].Time, 4);
        Assert.Equal((.4f, 1.2f), (clip.Contacts[0].Start, clip.Contacts[0].Finish));
        Assert.Equal(.8f, clip.Markers[0].Time, 4);
        clip.Validate(Model);
    }
}
