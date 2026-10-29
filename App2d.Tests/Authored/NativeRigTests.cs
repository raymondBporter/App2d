using App2d.Core.Characters.Authored;
using System.Numerics;
using System.Text.Json.Nodes;

namespace App2d.Tests.Authored;

/// <summary>
/// The native image-slot scene (<see cref="TestModels.Wheel"/>): bone nodes with a reflected, scaled root, image slots with
/// skins, tint and attachment timelines, draw order and events, round-tripping through the native file format. The scene's
/// inherited nonuniform scale and shear left with the affine adapter in P11c.
/// </summary>
public sealed class NativeRigTests
{
    private static (CharacterModel Model, IReadOnlyList<MotionClip> Animations) Scene() => TestModels.Wheel();

    [Fact]
    public void SamplingLeavesTheAuthoredModelUntouched()
    {
        var (source, clips) = Scene();
        var model = ResolvedModel.From(source);
        var before = source.ToJson();
        foreach (var time in new[] { 0f, .2f, .5f, .9f, 1f })
            PoseEvaluator.Sample(model, clips[0], time);
        Assert.Equal(before, source.ToJson());
    }

    [Fact]
    public void NativeRoundTripPreservesNamesFramesCurvesAndAppearanceTimelines()
    {
        var (source, clips) = Scene();
        var clip = clips[0];
        var loaded = CharacterModel.FromJson(source.ToJson());
        var motion = MotionClip.FromJson(clip.ToJson());
        var before = ResolvedModel.From(source);
        var after = ResolvedModel.From(loaded);
        motion.Validate(after);
        Assert.Equal(source.ToJson(), loaded.ToJson());
        Assert.Equal(clip.ToJson(), motion.ToJson());
        foreach (var time in new[] { .1f, .3f, .6f, .9f, 1f })
        {
            var a = PoseEvaluator.Sample(before, clip, time);
            var b = PoseEvaluator.Sample(after, motion, time);
            foreach (var bone in source.Controls)
            {
                TestModels.Near(a.World(bone.Id), b.World(bone.Id), 1e-6f);
                MatrixNear(a.Bones[bone.Id], b.Bones[bone.Id]);
            }
            Assert.Equal(a.Slots.Select(s => s.Part?.Id), b.Slots.Select(s => s.Part?.Id));
            Assert.Equal(a.Slots.Select(s => s.Slot.Id), b.Slots.Select(s => s.Slot.Id));
        }
    }

    [Theory]
    [InlineData("translate", .2f)]
    [InlineData("scale", 1f)]
    [InlineData("rotate", .7f)]
    public void EqualEndpointCurvesKeepOvershootAfterSavingAndRetiming(string kind, float control)
    {
        var (source, clips) = Scene();
        var clip = clips[0];
        clip.Tracks = [new() { Kind = kind, Target = "wheel-root", Keys =
            [new() { Curve = new() { Control1 = new(1f / 3, control), Control2 = new(2f / 3, control), Absolute = true } }, new() { Time = 1 }] }];
        var motion = MotionClip.FromJson(clip.ToJson());
        motion.Validate(ResolvedModel.From(source));
        var track = motion.Tracks[0];
        var sample = PoseEvaluator.Interpolate(track, .5f);
        Assert.Equal(.75f * control, kind == MotionClip.RotateKind ? sample.Angle : sample.Value.X, 5);
        if (kind == MotionClip.RotateKind)
        {
            Assert.Equal(Vector3.Zero, sample.Value);
        }

        ClipAuthoring.Retime(motion, 2);
        Assert.Equal(sample, PoseEvaluator.Interpolate(track, 1));
    }

    [Fact]
    public void NativeTintTimelinesKeepIndependentTimingAndReachTheDrawing()
    {
        var (source, clips) = Scene();
        var clip = clips[0];
        clip.Colors =
        [
            new() { Slot = "rear-paint", Kind = SlotColorTrack2D.Rgb, SetupBeforeFirst = true, Keys =
                [new() { Time = .25f, Value = new(1, 0, 0, 1)}, new() { Time = 1, Value = new(0, 1, 0, 1)}] },
            new() { Slot = "rear-paint", Kind = SlotColorTrack2D.Alpha, SetupBeforeFirst = true, Keys =
                [new() { Time = .5f, Value = new(1, 1, 1, .2f), Ease = ClipEase.Step }, new() { Time = .75f, Value = new(1, 1, 1, .8f) }] }
        ];
        var model = ResolvedModel.From(CharacterModel.FromJson(source.ToJson()));
        var motion = MotionClip.FromJson(clip.ToJson());
        motion.Validate(model);
        Vector4 At(float time) => PoseEvaluator.Sample(model, motion, time).Slots.Single(s => s.Slot.Id == "rear-paint").Color;
        Assert.Equal(Vector4.One, At(.1f));
        Assert.Equal(1, At(.4f).W);
        Assert.Equal(.2f, At(.6f).W);
        Assert.Equal(.8f, At(.75f).W);
        Assert.True(Vector4.Distance(new(.5f, .5f, 0, .2f), At(.625f)) < 1e-5f);
        var drawing = new PuppetDrawing();
        drawing.Build(model, PoseEvaluator.Sample(model, motion, .625f));
        Assert.Equal(6, drawing.Mesh.Count);
        foreach (var vertex in drawing.Mesh.Vertices)
        {
            Assert.InRange(vertex.Color.R, 127, 128);
            Assert.InRange(vertex.Color.G, 127, 128);
            Assert.Equal(0, vertex.Color.B);
            Assert.Equal(51, vertex.Color.A);
        }
        Assert.Equal("ffffffff", model.Base.Slots[0].Color);
    }

    [Fact]
    public void ReflectionAndUniformScaleComposeOntoTheChildAndACollapseStaysFinite()
    {
        var (Model, Animations) = Scene();
        var model = ResolvedModel.From(Model);
        var rest = PoseEvaluator.Rest(model);
        // The child's rest is its local origin through the root's reflected, scaled frame.
        var expected = Vector2.Transform(new(.4f, .05f), model.RestTransforms["wheel-root"]);
        TestModels.Near(new(expected, 0), rest.World("spoke-tip"));
        Assert.True(model.RestTransforms["wheel-root"].GetDeterminant() < 0, "the root reflects");
        Assert.Equal(1.5f, new Core.Mathematics.Transforms.Frame2D(model.RestTransforms["wheel-root"]).XAxis.Length(), 4);
        var clip = Animations.Single();
        var pose = PoseEvaluator.Sample(model, clip, 1);
        // At the end: the root turned by a further half turn and halved its pose scale; the child's setup-before-first translate holds its last key.
        Assert.Equal(.75f, new Core.Mathematics.Transforms.Frame2D(pose.Bones["wheel-root"]).XAxis.Length(), 4);
        Assert.True(pose.Bones["wheel-root"].GetDeterminant() < 0, "the reflection survives animation");
        expected = Vector2.Transform(new(.4f + .3f, .05f + .2f), pose.Bones["wheel-root"]);
        TestModels.Near(new(expected, 0), pose.World("spoke-tip"));
        clip.Tracks.Single(t => t.Kind == MotionClip.ScaleKind).Keys[^1].Value = new(-1, -1, 0);
        var collapsed = PoseEvaluator.Sample(model, clip, 1);
        Assert.Equal(0, collapsed.Bones["wheel-root"].GetDeterminant(), 5);
        Assert.True(float.IsFinite(collapsed.World("spoke-tip").X));
    }

    [Fact]
    public void SkinsFallbackAttachmentHidingOrderAndRepeatedEventsHaveIndependentMeaning()
    {
        var (Model, Animations) = Scene();
        var model = ResolvedModel.From(Model);
        var clip = Animations[0];
        var pose = PoseEvaluator.Sample(model, clip, .6, input: new()
        {
            Skin = "gold-finish"
        });
        Assert.Equal(["front-paint", "rear-paint"], pose.Slots.Select(s => s.Slot.Id));
        Assert.Null(pose.Slots[0].Part);
        Assert.Contains("gold-finish", pose.Slots[1].Part!.Id);
        pose = PoseEvaluator.Sample(model, clip, .9, input: new()
        {
            Skin = "gold-finish"
        });
        Assert.Contains("default", pose.Slots[0].Part!.Id);
        Assert.Equal([3, 5], clip.Events.Select(e => e.Int));
        Assert.Equal("tick", clip.Events[1].Name);
        var drawing = new PuppetDrawing();
        drawing.Build(model, pose);
        Assert.Equal(2, drawing.Batches.Count);
        Assert.All(drawing.Batches, b => Assert.NotNull(b.Texture));
        Assert.Equal(12, drawing.Mesh.Count);
    }

    [Fact]
    public void PoseEditingIsTheInverseOfEvaluationAndRestMovesRespectChildChoice()
    {
        var (Model, Animations) = Scene();
        var model = ResolvedModel.From(Model);
        var clip = Animations[0];
        var pose = PoseEvaluator.Sample(model, clip, .3);
        var desired = pose.World("spoke-tip") + new Vector3(.2f, -.15f, 0);
        ClipAuthoring.Pose(model, clip, pose, .3f, "spoke-tip", desired);
        TestModels.Near(desired, PoseEvaluator.Sample(model, clip, .3).World("spoke-tip"));
        var child = model.Rest["spoke-tip"];
        ModelAuthoring.MoveRest(Model, "wheel-root", new(1, 2, 0), children: false);
        var moved = ResolvedModel.From(Model);
        TestModels.Near(new(1, 2, 0), moved.Rest["wheel-root"]);
        TestModels.Near(child, moved.Rest["spoke-tip"]);
    }

    [Fact]
    public void VariantsAndFlatteningPreserveTheSetupAndPoseEditing()
    {
        var (Model, Animations) = Scene();
        // A bone root's local translation is a variant property (P6a): the override moves it and its child follows.
        var moved = new ModelVariant { Id = "moved-wheel", Name = "Moved wheel", Base = "wheel", Overrides = [PropertyOverride.Of(PropertyRef.OfControl("wheel-root", RigProperties.LocalX), 2)] };
        var shifted = ResolvedModel.From(Model, moved);
        Assert.Equal(2, shifted.Rest["wheel-root"].X, 5);
        TestModels.Near(ResolvedModel.From(Model).Rest["spoke-tip"] + new Vector3(1.5f, 0, 0), shifted.Rest["spoke-tip"]);
        var variant = new ModelVariant { Id = "moved-wheel", Name = "Moved wheel", Base = "wheel", Overrides = [PropertyOverride.Of(PropertyRef.OfPart(Model.Parts[0].Id, RigProperties.OffsetX), .1f)] };
        var model = ResolvedModel.From(Model, variant);
        var rest = PoseEvaluator.Rest(model);
        Assert.Equal(.1f, model.Parts[0].OffsetX);
        var flat = ResolvedModel.From(ModelAuthoring.Flatten(model, "flat-wheel", "Flat wheel"));
        Assert.Equal(.1f, flat.Parts[0].OffsetX);
        foreach (var bone in model.Order)
        {
            TestModels.Near(rest.World(bone.Id), PoseEvaluator.Rest(flat).World(bone.Id));
        }

        var clip = Animations[0];
        var pose = PoseEvaluator.Sample(model, clip, .3);
        var desired = pose.World("wheel-root") + new Vector3(.2f, .3f, 0);
        ClipAuthoring.Pose(model, clip, pose, .3f, "wheel-root", desired);
        TestModels.Near(desired, PoseEvaluator.Sample(model, clip, .3).World("wheel-root"));
    }

    [Fact]
    public void KeyPoseBeforeATimelineStartsKeepsSetupValues()
    {
        var (Model, Animations) = Scene();
        var clip = Animations[0];
        var model = ResolvedModel.From(Model);
        var expected = PoseEvaluator.Sample(model, clip, .1).World("spoke-tip");
        ClipAuthoring.KeyPose(clip, .1f);
        TestModels.Near(expected, PoseEvaluator.Sample(model, clip, .1).World("spoke-tip"));
    }

    [Fact]
    public void ReparentingKeepsWorldFrameAndVariantMovesCanLeaveChildrenBehind()
    {
        var source = Scene();
        var initial = ResolvedModel.From(source.Model);
        var oldFrame = initial.RestTransforms["spoke-tip"];
        ModelAuthoring.Reparent(source.Model, "spoke-tip", null);
        var moved = ResolvedModel.From(source.Model);
        MatrixNear(oldFrame, moved.RestTransforms["spoke-tip"]);
        TestModels.Near(initial.Rest["spoke-tip"], moved.Rest["spoke-tip"]);
        // A variant move without children re-expresses the child's setup so it stays in place (P6a).
        var variant = new ModelVariant { Id = "moved-wheel", Name = "Moved", Base = "wheel" };
        source = Scene();
        ModelAuthoring.MoveRest(ResolvedModel.From(source.Model), variant, "wheel-root", new(3, 4, 0), children: false);
        Assert.NotEmpty(variant.Overrides);
        var left = ResolvedModel.From(source.Model, variant);
        TestModels.Near(new(3, 4, 0), left.Rest["wheel-root"]);
        TestModels.Near(initial.Rest["spoke-tip"], left.Rest["spoke-tip"]);
    }

    [Theory]
    [InlineData("model", 2)]
    [InlineData("model", 11)]
    [InlineData("clip", 1)]
    [InlineData("clip", 3)]
    [InlineData("variant", 5)]
    public void PreviousNativeFormatsAreRejected(string kind, int version)
    {
        // P11c retired the legacy readers: a file below its kind's current version is refused by version, never translated.
        var source = TestModels.Creature();
        var json = kind switch
        {
            "model" => source.ToJson(),
            "clip" => TestModels.Clip(ResolvedModel.From(source)).ToJson(),
            _ => new ModelVariant { Id = "other", Name = "Other", Base = source.Id }.ToJson()
        };
        var node = JsonNode.Parse(json)!;
        node["version"] = version;
        var error = Assert.Throws<InvalidDataException>(() =>
        {
            switch (kind)
            {
                case "model":
                    CharacterModel.FromJson(node.ToJsonString());
                    break;
                case "clip":
                    MotionClip.FromJson(node.ToJsonString());
                    break;
                default:
                    ModelVariant.FromJson(node.ToJsonString());
                    break;
            }
        });
        Assert.Contains("unsupported format/version", error.Message);
    }

    private static void MatrixNear(Matrix3x2 a, Matrix3x2 b)
    {
        Assert.InRange(MathF.Abs(a.M11 - b.M11), 0, 2e-5f);
        Assert.InRange(MathF.Abs(a.M12 - b.M12), 0, 2e-5f);
        Assert.InRange(MathF.Abs(a.M21 - b.M21), 0, 2e-5f);
        Assert.InRange(MathF.Abs(a.M22 - b.M22), 0, 2e-5f);
        Assert.InRange(MathF.Abs(a.M31 - b.M31), 0, 2e-5f);
        Assert.InRange(MathF.Abs(a.M32 - b.M32), 0, 2e-5f);
    }
}
