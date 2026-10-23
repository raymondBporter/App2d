using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using System.Numerics;

namespace App2d.Tests.Authored;

public sealed class AnimationEditingTests
{
    private static (ResolvedModel Model, MotionClip Clip) Scene()
    {
        var source = TestModels.Creature();
        source.Slots = [.. new[] { "back", "middle", "front" }.Select(id => new SkeletonSlot2D
        {
            Id = id, Bone = "hip", Attachment = "art"
        })];
        source.Skins = [new() { Attachments = source.Slots.ToDictionary(s => s.Id, _ => new Dictionary<string, string> { ["art"] = "torso" }) }];
        var model = ResolvedModel.From(source);
        return (model, TestModels.Clip(model));
    }

    [Fact]
    public void AttachmentEditsReplaceNearbyKeysAndDeletingRestoresTheHeldChoice()
    {
        var (model, clip) = Scene();
        ClipAuthoring.SetAttachmentKey(clip, "middle", .75f, "art");
        ClipAuthoring.SetAttachmentKey(clip, "middle", .25f, "art");
        ClipAuthoring.SetAttachmentKey(clip, "middle", .25f + ClipAuthoring.SameTime / 2, null);
        Assert.Equal(2, clip.Attachments.Single().Keys.Count);
        Assert.Null(PoseEvaluator.Sample(model, clip, .5).Slots.Single(s => s.Slot.Id == "middle").Part);
        Assert.NotNull(PoseEvaluator.Sample(model, clip, .8).Slots.Single(s => s.Slot.Id == "middle").Part);
        ClipAuthoring.DeleteAttachmentKey(clip, "middle", .25f);
        Assert.NotNull(PoseEvaluator.Sample(model, clip, .5).Slots.Single(s => s.Slot.Id == "middle").Part);
        ClipAuthoring.DeleteAttachmentKey(clip, "middle", .75f);
        Assert.Empty(clip.Attachments);
        clip.Validate(model);
    }

    [Fact]
    public void DrawOrderEditsStartFromTheHeldOrderWithoutMutatingEarlierKeys()
    {
        var (model, clip) = Scene();
        Assert.False(ClipAuthoring.MoveSlot(clip, model.Base.Slots, "back", .1f, -1));
        Assert.False(ClipAuthoring.MoveSlot(clip, model.Base.Slots, "missing", .1f, 1));
        Assert.Empty(clip.DrawOrder);
        Assert.True(ClipAuthoring.MoveSlot(clip, model.Base.Slots, "back", .25f, 1));
        Assert.True(ClipAuthoring.MoveSlot(clip, model.Base.Slots, "back", .75f, 1));
        Assert.Equal(["middle", "back", "front"], clip.DrawOrder[0].Slots);
        Assert.Equal(["middle", "front", "back"], PoseEvaluator.Sample(model, clip, .8).Slots.Select(s => s.Slot.Id));
        ClipAuthoring.DeleteDrawOrderKey(clip, .75f);
        Assert.Equal(["middle", "back", "front"], PoseEvaluator.Sample(model, clip, .8).Slots.Select(s => s.Slot.Id));
        Assert.Equal(["back", "middle", "front"], model.Base.Slots.Select(s => s.Id));
        clip.Validate(model);
    }

    [Fact]
    public void TintCurvesKeepIndependentAlphaAndDoNotRetainTheCallersMutableCurve()
    {
        var (model, clip) = Scene();
        ClipAuthoring.SetColorKey(clip, "middle", SlotColorTrack2D.Rgb, 0, new(0, .3f, .5f, 1));
        ClipAuthoring.SetColorKey(clip, "middle", SlotColorTrack2D.Rgb, 1, new(1, .3f, .5f, 1));
        ClipAuthoring.SetColorKey(clip, "middle", SlotColorTrack2D.Alpha, 0, new(1, 1, 1, .2f));
        ClipAuthoring.SetColorKey(clip, "middle", SlotColorTrack2D.Alpha, 1, new(1, 1, 1, .8f));
        ClipAuthoring.SetColorEase(clip, "middle", SlotColorTrack2D.Rgb, 0, ClipEase.Step);
        var curve = new KeyCurve2D { Control1 = new(1f / 3, 1f / 3), Control2 = new(2f / 3, 2f / 3) };
        ClipAuthoring.SetColorCurve(clip, "middle", SlotColorTrack2D.Rgb, 0, 0, curve);
        curve.Control1 = curve.Control1 with { Y = 99 };
        var slot = model.Base.Slots[1];
        Assert.InRange(Vector4.Distance(new(.5f, .3f, .5f, .5f), SlotColorTrack2D.Evaluate(clip, slot, .5f)), 0, 1e-5f);
        ClipAuthoring.SetColorEase(clip, "middle", SlotColorTrack2D.Rgb, 0, ClipEase.Step);
        Assert.Null(clip.Colors[0].Keys[0].CurveR);
        Assert.InRange(Vector4.Distance(new(0, .3f, .5f, .5f), SlotColorTrack2D.Evaluate(clip, slot, .5f)), 0, 1e-5f);
        ClipAuthoring.DeleteColorKey(clip, "middle", SlotColorTrack2D.Rgb, 0);
        ClipAuthoring.DeleteColorKey(clip, "middle", SlotColorTrack2D.Rgb, 1);
        Assert.Equal(SlotColorTrack2D.Alpha, Assert.Single(clip.Colors).Kind);
        clip.Validate(model);
    }

    [Fact]
    public void ComponentEditsPreserveTheOtherSampledComponents()
    {
        var (_, clip) = Scene();
        var channel = new Channel(MotionClip.TranslateKind, "body");
        ClipAuthoring.SetKey(clip, channel, 0, new(0, 2, 4));
        ClipAuthoring.SetKey(clip, channel, 1, new(2, 4, 6));
        ClipAuthoring.SetComponentKey(clip, channel, .5f, 0, 8);
        TestModels.Near(new(8, 3, 5), ClipAuthoring.Value(clip, channel, .5f).Value);
        ClipAuthoring.SetComponentKey(clip, channel, .5f, 1, -3);
        TestModels.Near(new(8, -3, 5), ClipAuthoring.Value(clip, channel, .5f).Value);
    }

    [Fact]
    public void ControlChannelGroupsRespectIkEndsAndSolvedJoints()
    {
        var (model, _) = Scene();
        Assert.Equal([new Channel(MotionClip.TargetKind, "arm")], ClipAuthoring.ChannelsForControl(model, "hand"));
        Assert.Empty(ClipAuthoring.ChannelsForControl(model, "elbow"));
        // A canonical node has a uniform pose scale; shear is the legacy affine adapter's channel.
        Assert.Equal([MotionClip.TranslateKind, MotionClip.RotateKind, MotionClip.ScaleKind],
            ClipAuthoring.ChannelsForControl(model, "body").Select(c => c.Kind));
    }

    [Fact]
    public void ContactRangeEditsStayInsideTheClip()
    {
        var (_, clip) = Scene();
        clip.Contacts.Add(new()
        {
            Chain = "leg",
            Start = .2f,
            Finish = .4f
        });
        ClipAuthoring.SetContactRange(clip, 0, new(-1, 2));
        Assert.Equal(0, clip.Contacts[0].Start);
        Assert.Equal(clip.Duration, clip.Contacts[0].Finish);
        ClipAuthoring.SetContactRange(clip, 0, new(.8f, .1f));
        Assert.Equal(.1f, clip.Contacts[0].Finish);
        Assert.Equal(.09f, clip.Contacts[0].Start, 5);
    }

    [Fact]
    public void SteadyTravelCanChangeDistanceAndReturnToAnEmptyTrack()
    {
        var (_, clip) = Scene();
        ClipAuthoring.SetSteadyTravel(clip, 3);
        ClipAuthoring.Retime(clip, 2);
        Assert.True(ClipAuthoring.TryGetSteadyTravel(clip, out var distance));
        Assert.Equal(3, distance);
        Assert.Equal(1.5f, PoseEvaluator.Interpolate(clip.Travel.Keys, 1).Value.X);
        ClipAuthoring.SetSteadyTravel(clip, 0);
        Assert.Empty(clip.Travel.Keys);
    }

    [Theory]
    [InlineData("curve")]
    [InlineData("ease")]
    [InlineData("depth")]
    [InlineData("extra-key")]
    public void SimpleTravelEditsCannotFlattenAuthoredMotion(string kind)
    {
        var (_, clip) = Scene();
        ClipAuthoring.SetSteadyTravel(clip, 3);
        switch (kind)
        {
            case "curve":
                clip.Travel.Keys[0].Curve = new()
                {
                    Control1 = new(0, .5f)
                };
                break;
            case "ease":
                clip.Travel.Keys[0].Ease = ClipEase.Smooth;
                break;
            case "depth":
                clip.Travel.Keys[1].Value = clip.Travel.Keys[1].Value with { Z = 1 };
                break;
            case "extra-key":
                clip.Travel.Keys.Insert(1, new()
                {
                    Time = .5f,
                    Value = new(1, 0, 0)
                });
                break;
        }
        var before = clip.ToJson();
        Assert.False(ClipAuthoring.TryGetSteadyTravel(clip, out _));
        Assert.Throws<InvalidOperationException>(() => ClipAuthoring.SetSteadyTravel(clip, 4));
        Assert.Equal(before, clip.ToJson());
    }

    [Fact]
    public void CurveUnitConversionPreservesSamplesAndRefusesUnrepresentableEqualEndpoints()
    {
        var curve = new KeyCurve2D { Control1 = new(.2f, -.5f), Control2 = new(.8f, 1.5f) };
        Assert.True(curve.TryConvertValueControls(true, 2, 5, out var absolute));
        Assert.True(absolute.TryConvertValueControls(false, 2, 5, out var restored));
        foreach (var time in new[] { .1f, .3f, .5f, .8f })
        {
            Assert.Equal(curve.Evaluate(time, 2, 5), absolute.Evaluate(time, 2, 5), 5);
            Assert.Equal(curve.Evaluate(time, 2, 5), restored.Evaluate(time, 2, 5), 5);
        }
        var overshoot = new KeyCurve2D { Absolute = true, Control1 = new(0, 3), Control2 = new(1, 3) };
        Assert.False(overshoot.TryConvertValueControls(false, 2, 2, out _));
        Assert.True(new KeyCurve2D { Absolute = true, Control1 = new(0, 2), Control2 = new(1, 2) }.TryConvertValueControls(false, 2, 2, out _));
    }

    [Fact]
    public void SlotAndControlSelectionReplaceEachOther()
    {
        var selection = new Selection { Control = "body", Key = .5f, Action = "attack" };
        selection.Select(slot: "middle");
        Assert.Null(selection.Control);
        Assert.Null(selection.Key);
        Assert.Null(selection.Action);
        selection.Select(control: "body");
        Assert.Null(selection.Slot);
        selection.Select(slot: "front");
        selection.Clear();
        Assert.Null(selection.Slot);
    }

    [Fact]
    public void SourcePreviewRebuildsItsReferenceFitWhenThePreviewModelChanges()
    {
        var sources = SourceLibraries.Open(Path.GetDirectoryName(TestModels.AuthoredRoot)!);
        var source = PersonTemplate.Model();
        var library = PointLibrary.Load(TestAssets.GetPath("Characters", "person", "library.json"));
        var clip = LibraryImport.Convert(library, "idle", source, LibraryImport.DefaultMapping(source, library), "source-idle", "Idle", restClip: "idle");
        var model = ResolvedModel.From(source);
        var preview = new SourceMotionPreview(sources);
        var first = preview.Sample(new(model.Base.Id, model, PoseEvaluator.Rest(model), clip), clip, 0).ToDictionary(p => p.Control, p => p.Point);
        // Bone nodes (the left arm) derive their rest, so they follow their moved parents.
        foreach (var control in source.Controls.Where(c => c.Node == RigNodeKinds.Point))
        {
            control.Rest = PuppetPoint.From(control.Rest.XYZ + new Vector3(3, 0, 0));
        }

        var moved = ResolvedModel.From(source);
        var second = preview.Sample(new(moved.Base.Id, moved, PoseEvaluator.Rest(moved), clip), clip, 0).ToDictionary(p => p.Control, p => p.Point);
        Assert.NotEmpty(first);
        foreach (var (control, point) in first)
        {
            TestModels.Near(point + new Vector3(3, 0, 0), second[control]);
        }
    }
}
