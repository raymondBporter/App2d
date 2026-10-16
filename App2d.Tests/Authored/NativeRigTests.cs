using App2d.Core.Characters.Authored;
using App2d.Core.Rendering.Characters;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json.Nodes;

namespace App2d.Tests.Authored;

public sealed class NativeRigTests
{
    private static (CharacterModel Model, IReadOnlyList<MotionClip> Animations) Scene()
    {
        var model = new CharacterModel
        {
            Id = "wheel",
            Name = "Wheel",
            Controls =
            [
                new() { Id = "wheel-root", Name = "Wheel Root", Transform = new()
                    { X = .5f, Y = .7f, Rotation = MathF.PI / 6, ScaleX = -1.5f, ScaleY = 2, ShearY = MathF.PI / 12 } },
                new() { Id = "spoke-tip", Name = "spoke.tip", Parent = "wheel-root", Transform = new() { X = .4f, Y = .05f, Rotation = MathF.PI / 9 } }
            ],
            Slots =
            [
                new() { Id = "rear-paint", Bone = "wheel-root", Attachment = "red" },
                new() { Id = "front-paint", Bone = "spoke-tip", Attachment = "blue" }
            ],
            Skins =
            [
                new() { Attachments = { ["rear-paint"] = new() { ["red"] = "default-red" }, ["front-paint"] = new() { ["blue"] = "default-blue" } } },
                new() { Id = "gold-finish", Attachments = { ["rear-paint"] = new() { ["red"] = "gold-finish-red" } } }
            ]
        };
        foreach (var (id, bone) in new[] { ("default-red", "wheel-root"), ("default-blue", "spoke-tip"), ("gold-finish-red", "wheel-root") })
        {
            model.Parts.Add(new()
            {
                Id = id,
                A = bone,
                Frame = bone,
                Width = .16f,
                Height = .2f,
                Geometry = new Rectangle2D.Builder { Min = new(-.5f, -.5f), Max = new(.5f, .5f) },
                Material = new()
                {
                    Texture = "images/" + id + ".png"
                }
            });
        }

        var clip = ClipAuthoring.New(ResolvedModel.From(model), "wheel-turn", "Turn", loop: false);
        clip.Tracks =
        [
            new() { Kind = MotionClip.RotateKind, Target = "wheel-root", Keys =
                [new() { Curve = new() { X1 = .2f, X2 = .8f, Y1 = 40 * MathF.PI / 180, Y2 = 140 * MathF.PI / 180, Absolute = true } }, new() { Time = 1, Angle = MathF.PI }] },
            new() { Kind = MotionClip.ScaleKind, Target = "wheel-root", Keys = [new(), new() { Time = 1, X = -.5f, Y = .5f }] },
            new() { Kind = MotionClip.ShearKind, Target = "wheel-root", Keys = [new(), new() { Time = 1, X = MathF.PI / 18, Y = -MathF.PI / 9 }] },
            new() { Kind = MotionClip.TranslateKind, Target = "spoke-tip", SetupBeforeFirst = true, Keys = [new() { Time = .25f, X = .2f, Y = .1f }, new() { Time = 1, X = .3f, Y = .2f }] }
        ];
        clip.Attachments = [new() { Slot = "front-paint", Keys = [new() { Time = .5f }, new() { Time = .8f, Attachment = "blue" }] }];
        clip.DrawOrder = [new() { Time = .4f, Slots = ["front-paint", "rear-paint"] }];
        clip.Events = [new() { Time = .2f, Name = "tick", Int = 3 }, new() { Time = .8f, Name = "tick", Int = 5 }];
        clip.Validate(ResolvedModel.From(model));
        return (model, [clip]);
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
    [InlineData("shear", .7f)]
    [InlineData("rotate", .7f)]
    public void EqualEndpointCurvesKeepOvershootAfterSavingAndRetiming(string kind, float control)
    {
        var (source, clips) = Scene();
        var clip = clips[0];
        clip.Tracks = [new() { Kind = kind, Target = "wheel-root", Keys =
            [new() { Curve = new() { X1 = 1f / 3, X2 = 2f / 3, Y1 = control, Y2 = control, Absolute = true } }, new() { Time = 1 }] }];
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
                [new() { Time = .25f, R = 1, G = 0, B = 0 }, new() { Time = 1, R = 0, G = 1, B = 0 }] },
            new() { Slot = "rear-paint", Kind = SlotColorTrack2D.Alpha, SetupBeforeFirst = true, Keys =
                [new() { Time = .5f, A = .2f, Ease = ClipEase.Step }, new() { Time = .75f, A = .8f }] }
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
            Assert.InRange((int)vertex.Color.R, 127, 128);
            Assert.InRange((int)vertex.Color.G, 127, 128);
            Assert.Equal(0, vertex.Color.B);
            Assert.Equal(51, vertex.Color.A);
        }
        Assert.Equal("ffffffff", model.Base.Slots[0].Color);
    }

    [Fact]
    public void AffineChannelsComposeReflectShearAndCollapseWithoutLosingDescendantFrames()
    {
        var (Model, Animations) = Scene();
        var model = ResolvedModel.From(Model);
        var setup = Model.Controls[0].Transform!;
        var rest = PoseEvaluator.Rest(model);
        var expected = Vector2.Transform(new(.4f, .05f), setup.Matrix);
        TestModels.Near(new(expected, 0), rest.World("spoke-tip"));
        var clip = Animations.Single();
        var pose = PoseEvaluator.Sample(model, clip, 1);
        var animated = setup with
        {
            Rotation = setup.Rotation + MathF.PI,
            ScaleX = setup.ScaleX * .5f,
            ScaleY = setup.ScaleY * 1.5f,
            ShearX = 10 * MathF.PI / 180,
            ShearY = setup.ShearY - 20 * MathF.PI / 180
        };
        expected = Vector2.Transform(new(.7f, .25f), animated.Matrix);
        TestModels.Near(new(expected, 0), pose.World("spoke-tip"));
        clip.Tracks.Single(t => t.Kind == MotionClip.ScaleKind).Keys[^1].X = -1;
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
    public void AffinePoseEditingIsTheInverseOfEvaluationAndRestMovesRespectChildChoice()
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
    public void VariantsAndFlatteningPreserveAffineSetupAndPoseEditing()
    {
        var (Model, Animations) = Scene();
        var variant = new ModelVariant { Id = "moved-wheel", Name = "Moved wheel", Base = "wheel", Rest = new() { ["wheel-root"] = new(2, 3) } };
        var model = ResolvedModel.From(Model, variant);
        var rest = PoseEvaluator.Rest(model);
        TestModels.Near(new(2, 3, 0), rest.World("wheel-root"));
        var flat = ResolvedModel.From(ModelAuthoring.Flatten(model, "flat-wheel", "Flat wheel"));
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
    public void AffineReparentingKeepsWorldFrameAndVariantMovesCanLeaveChildrenBehind()
    {
        var source = Scene();
        var initial = ResolvedModel.From(source.Model);
        var oldFrame = initial.RestTransforms["spoke-tip"];
        ModelAuthoring.Reparent(source.Model, "spoke-tip", null);
        var moved = ResolvedModel.From(source.Model);
        MatrixNear(oldFrame, moved.RestTransforms["spoke-tip"]);
        TestModels.Near(initial.Rest["spoke-tip"], moved.Rest["spoke-tip"]);
        var variant = new ModelVariant { Id = "moved-wheel", Name = "Moved", Base = "wheel" };
        source = Scene();
        ModelAuthoring.MoveRest(ResolvedModel.From(source.Model), variant, "wheel-root", new(3, 4, 0), children: false);
        TestModels.Near(initial.Rest["spoke-tip"], PoseEvaluator.Rest(ResolvedModel.From(source.Model, variant)).World("spoke-tip"));
    }

    [Theory]
    [InlineData("model", 2)]
    [InlineData("model", 3)]
    [InlineData("clip", 1)]
    [InlineData("clip", 2)]
    [InlineData("variant", 2)]
    public void PreviousNativeFormatsAreRejected(string kind, int version)
    {
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
