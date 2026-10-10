using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using App2d.Core.Mathematics;
using App2d.Core.Rendering.Characters;
using System.Numerics;
using System.Text.Json;

namespace App2d.Tests.Authored;

public sealed class RigConstraintTests
{
    [Fact]
    public void TypedPointConstraintsPreserveLegacyContactsFramesAndMasks()
    {
        var original = TestModels.Creature();
        var converted = CharacterModel.FromJson(original.ToJson());
        converted.Constraints.AddRange(converted.Chains); converted.Chains.Clear();
        var before = ResolvedModel.From(original); var after = ResolvedModel.From(CharacterModel.FromJson(converted.ToJson()));
        var clip = TestModels.Clip(before); clip.Travel = new() { Scale = "leg", Keys = [new(), new() { Time = 1, X = .15f }] };
        clip.Tracks = [new() { Kind = MotionClip.RotateKind, Target = "body", Keys = [new() { Angle = .13f }] }];
        clip.Contacts = [new() { Chain = "leg", Start = .2f, Finish = .7f, Target = new(.03f, .01f, .1f) }];
        clip.Validate(after);
        foreach (var time in new[] { 0, .2, .35, .69999, .7, 1, 2.35 })
        foreach (var weight in new[] { 0, .5f, 1 })
        {
            var input = new PoseInput
            {
                InPlace = true, ReverseHorizontalMotion = true, Contact = (_, p) => p + new Vector3(.01f, .02f, 0),
                Overlay = new(clip, .3, new HashSet<string> { "leg" }, weight)
            };
            var a = PoseEvaluator.Sample(before, clip, time, true, input); var b = PoseEvaluator.Sample(after, clip, time, true, input);
            foreach (var control in before.Order)
            {
                Assert.Equal(a.Points[control.Id], b.Points[control.Id]); Assert.Equal(a.Bones[control.Id], b.Bones[control.Id]);
                Assert.Equal(a.Angles[control.Id], b.Angles[control.Id]);
            }
            Assert.Equal(a.Chains, b.Chains); Assert.Equal(a.Contacts, b.Contacts);
        }
    }

    [Theory, InlineData(true), InlineData(false)]
    public void BoneIkTurnsSegmentsBranchesAttachmentsAndSockets(bool affine)
    {
        var model = Machine(affine); var resolved = ResolvedModel.From(model); var clip = Aim(resolved, new(1, 1, .4f));
        var pose = PoseEvaluator.Sample(resolved, clip, .5);
        TestModels.Near(new(0, 1, .1f), pose.World("hinge")); TestModels.Near(new(1, 1, .4f), pose.World("tool"));
        TestModels.Near(new(-.25f, 0, 0), pose.World("flag"));
        Assert.Equal(MathF.PI / 2, pose.Angles["beam"], 4); Assert.Equal(0, pose.Angles["hinge"], 4);
        var local = Rotation2D.Apply(new(.2f, .3f), .3f);
        TestModels.Near(new(1 + local.X, 1 + local.Y, .5f), pose.World("payload"));
        var part = model.Parts[0];
        var frame = PartGeometry.FrameOf(part, pose.World, id => pose.Angles[id], id => pose.Bones[id]);
        TestModels.Near(new(0, .5f, 0), frame.Origin);
        var socket = new ActorPose(pose, Vector2.Zero, 1).Socket(model.Sockets[0]);
        var offset = Rotation2D.Apply(new(.2f, 0), .3f);
        TestModels.Near(new(1 + offset.X, 1 + offset.Y, .4f), socket.Origin);
        Assert.All(pose.Chains, c => Assert.True(c.Reached));
        AssertFramesAgree(pose);
        var drawing = new PuppetDrawing(); drawing.Build(resolved, pose); Assert.True(drawing.Mesh.Count > 0);
    }

    [Theory, InlineData(2, 2), InlineData(-2, 2), InlineData(2, -2), InlineData(-2, -2)]
    public void UniformScaleAndReflectionsCarryBendAndDescendants(float x, float y)
    {
        var model = Machine();
        model.Controls.Single(c => c.Id == "carrier").Transform = new() { ScaleX = x, ScaleY = y };
        var resolved = ResolvedModel.From(model); var clip = Aim(resolved, new(x, y, .2f));
        var pose = PoseEvaluator.Sample(resolved, clip, 0);
        TestModels.Near(new(0, y, .1f), pose.World("hinge")); TestModels.Near(new(x, y, .2f), pose.World("tool"));
        TestModels.Near(new(-.25f * x, 0, 0), pose.World("flag"));
        Assert.InRange(MathF.Abs(Vector2.Distance(XY(pose.World("beam")), XY(pose.World("hinge"))) - MathF.Abs(x)), 0, 1e-4f);
        Assert.Equal(Math.Sign(x * y), Math.Sign(pose.Bones["hinge"].GetDeterminant()));
        AssertFramesAgree(pose);
    }

    [Fact]
    public void BoneTargetsUseTheFullFreeFrameAndDraggingIsItsInverse()
    {
        var model = Machine(); model.IkChains.Single().Frame = "carrier";
        model.Controls[0].Transform = new() { X = .4f, Y = -.2f, Rotation = .4f, ScaleX = -2, ScaleY = 2 };
        var resolved = ResolvedModel.From(model); var clip = ClipAuthoring.New(resolved, "machine-move", "Move");
        ClipAuthoring.SetKey(clip, new(MotionClip.RotateKind, "carrier"), 0, default, .2f);
        ClipAuthoring.SetKey(clip, new(MotionClip.ScaleKind, "carrier"), 0, new(.5f, .5f, 0));
        ClipAuthoring.SetKey(clip, new(MotionClip.TranslateKind, "carrier"), 0, new(.1f, .15f, .05f));
        var before = PoseEvaluator.Sample(resolved, clip, 0);
        var desired = new Vector3(Vector2.Transform(new(1, 1), before.Bones["carrier"]), .3f);
        ClipAuthoring.Pose(resolved, clip, before, 0, "tool", desired);
        clip.Validate(resolved);
        var reloaded = MotionClip.FromJson(clip.ToJson());
        var pose = PoseEvaluator.Sample(resolved, reloaded, 0);
        TestModels.Near(desired, pose.World("tool"));
        TestModels.Near(new(-1, 1, .05f), ClipAuthoring.Value(reloaded, new(MotionClip.TargetKind, "reach"), 0).Value);
    }

    [Fact]
    public void ContactPinsHoldBoneTargetsAndReleaseBeforeSolvingAttachments()
    {
        var resolved = ResolvedModel.From(Machine()); var clip = Aim(resolved, new(1, 1, .2f));
        clip.Travel.Keys = [new(), new() { Time = 1, X = .2f }];
        clip.Contacts = [new() { Chain = "reach", Start = .1f, Finish = 1, Target = new(-1, 1) }]; clip.Loop = false;
        clip.Validate(resolved);
        var calls = 0;
        var pose = PoseEvaluator.Sample(resolved, clip, .5, input: new() { Contact = (_, p) => { calls++; return p + new Vector3(.03f, .01f, 0); } });
        TestModels.Near(new(1.03f, 1.01f, .2f), pose.World("tool")); Assert.Single(pose.Contacts); Assert.Equal(1, calls);
        AssertFramesAgree(pose);
        Assert.Single(PoseEvaluator.Sample(resolved, clip, 3).Contacts);
        var overlay = Aim(resolved, new(1.2f, .8f, .2f));
        var fading = PoseEvaluator.Sample(resolved, clip, .5, input: new()
        {
            Contact = (_, p) => { calls++; return p; }, Overlay = new(overlay, .5, new HashSet<string> { "reach" }, .5f)
        });
        TestModels.Near(new(1.1f, .95f, .2f), fading.World("tool")); Assert.Empty(fading.Contacts); Assert.Equal(1, calls);
        var released = PoseEvaluator.Sample(resolved, clip, .5, input: new() { Overlay = new(overlay, .5, new HashSet<string> { "reach" }) });
        TestModels.Near(new(1.3f, .8f, .2f), released.World("tool")); Assert.Empty(released.Contacts);
        clip.Loop = true; Assert.Empty(PoseEvaluator.Sample(resolved, clip, 1, repeat: true).Contacts);
    }

    [Fact]
    public void SeekingScalingAndUnreachableGoalsPreserveLengthsWithoutAccumulatingState()
    {
        var model = Machine(); var json = model.ToJson(); var resolved = ResolvedModel.From(model);
        var clip = Aim(resolved, new(8, 1, .2f));
        ClipAuthoring.SetKey(clip, new(MotionClip.ScaleKind, "beam"), 0, default);
        ClipAuthoring.SetKey(clip, new(MotionClip.ScaleKind, "beam"), 1, new(1, 1, 0)); clip.Validate(resolved);
        foreach (var t in new[] { .7f, .1f, .7f, 1, 0, .5f })
        {
            var pose = PoseEvaluator.Sample(resolved, clip, t); Assert.False(pose.Chains.Single().Reached);
            Assert.Equal(1 + t, Vector2.Distance(XY(pose.World("beam")), XY(pose.World("hinge"))), 4);
            Assert.Equal(1 + t, Vector2.Distance(XY(pose.World("hinge")), XY(pose.World("tool"))), 4);
            AssertFramesAgree(pose);
        }
        Assert.Equal(json, model.ToJson());
    }

    [Fact]
    public void StraightAndFullyFoldedBonesKeepTheirExactRestLengths()
    {
        var model = ResolvedModel.From(Machine()); var rest = PoseEvaluator.Rest(model);
        TestModels.Near(new(1, 0, .1f), rest.World("hinge"), 1e-6f);
        TestModels.Near(new(2, 0, .2f), rest.World("tool"), 1e-6f);
        var folded = PoseEvaluator.Sample(model, Aim(model, new(0, 0, .2f)), 0);
        TestModels.Near(new(0, 0, .2f), folded.World("tool"), 1e-6f);
        Assert.Equal(1, Vector2.Distance(XY(folded.World("hinge")), XY(folded.World("tool"))), 5);
    }

    [Theory, InlineData(-2, 2), InlineData(2, -2), InlineData(-2, -2)]
    public void AContactAtTheRootKeepsTheMirroredFoldDirection(float x, float y)
    {
        var model = Machine(); model.Controls[0].Transform = new() { ScaleX = x, ScaleY = y };
        var resolved = ResolvedModel.From(model); var clip = Aim(resolved, new(x, y, .2f));
        clip.Contacts = [new() { Chain = "reach", Finish = 1, Target = new(-2 * x, 0) }];
        clip.Validate(resolved);
        var pose = PoseEvaluator.Sample(resolved, clip, .5);
        TestModels.Near(new(0, 0, .2f), pose.World("tool")); TestModels.Near(new(0, y, .1f), pose.World("hinge"));
    }

    [Theory, InlineData("nonuniform"), InlineData("shear"), InlineData("collapsed"), InlineData("detached"), InlineData("own-frame")]
    public void InvalidBoneSetupsAreRejectedWithTheConstraintName(string edit)
    {
        var model = Machine();
        var beam = model.Controls.Single(c => c.Id == "beam");
        switch (edit)
        {
            case "nonuniform": beam.Transform!.ScaleX = 2; break;
            case "shear": beam.Transform!.ShearY = .2f; break;
            case "collapsed": beam.Transform!.ScaleX = beam.Transform.ScaleY = 0; break;
            case "detached": model.Controls.Single(c => c.Id == "hinge").Transform!.Y = .1f; break;
            case "own-frame": model.IkChains.Single().Frame = "beam"; break;
        }
        Assert.Contains("reach", Assert.Throws<InvalidDataException>(model.Validate).Message);
    }

    [Theory, InlineData(1, 0), InlineData(-1, -1), InlineData(-.99999f, -.99999f)]
    public void UnsupportedAnimatedScalesFailExplicitly(float x, float y)
    {
        var resolved = ResolvedModel.From(Machine()); var clip = Aim(resolved, new(1, 1, .2f));
        ClipAuthoring.SetKey(clip, new(MotionClip.ScaleKind, "carrier"), 0, new(x, y, 0)); clip.Validate(resolved);
        Assert.Contains("reach", Assert.Throws<InvalidDataException>(() => PoseEvaluator.Sample(resolved, clip, 0)).Message);
    }

    [Fact]
    public void BoneVariantOffsetsCannotSilentlyChangeTheDeclaredLengths()
    {
        var variant = new ModelVariant { Id = "bad-offset", Name = "Offset", Base = "machine", Rest = { ["hinge"] = new(1, .2f, .1f) } };
        Assert.Contains("+X tip", Assert.Throws<InvalidDataException>(() => ResolvedModel.From(Machine(), variant)).Message);
    }

    [Theory, InlineData(true), InlineData(false)]
    public void RestLengthEditsCarryTheConnectedJointAndCanStillRotate(bool affine)
    {
        var model = Machine(affine);
        ModelAuthoring.ResizeBone(model, "beam", 1.5f);
        ModelAuthoring.RotateRestBone(model, "beam", .4f);
        var resolved = ResolvedModel.From(model); var pose = PoseEvaluator.Rest(resolved);
        TestModels.Near(new(Rotation2D.Apply(new(1.5f, 0), .4f), .1f), pose.World("hinge"));
        TestModels.Near(new(Rotation2D.Apply(new(2.5f, 0), .4f), .2f), pose.World("tool"));
    }

    [Fact]
    public void OverlappingConstraintsAndTracksCannotCompeteForSolvedFrames()
    {
        var model = Machine(); var original = model.IkChains.Single();
        model.Constraints.Add(original with { Id = "second" });
        Assert.Throws<InvalidDataException>(model.Validate);
        model.Constraints.RemoveAt(1); var resolved = ResolvedModel.From(model);
        foreach (var channel in new[] { new Channel(MotionClip.RotateKind, "beam"), new(MotionClip.TranslateKind, "hinge"), new(MotionClip.ScaleKind, "tool") })
        {
            var clip = ClipAuthoring.New(resolved, "competing", "Competing"); ClipAuthoring.SetKey(clip, channel, 0, default);
            Assert.Contains("IK", Assert.Throws<InvalidDataException>(() => clip.Validate(resolved)).Message);
        }
    }

    [Fact]
    public void SchemaIsStrictAndLegacyVersionsUpgradeWithoutCreatingConstraints()
    {
        var json = Machine().ToJson();
        Assert.Contains("\"kind\": \"two-bone-ik\"", json); Assert.Contains("\"solver\": \"bone\"", json);
        Assert.Equal(json, CharacterModel.FromJson(json).ToJson());
        var reordered = json.Replace("\"kind\": \"two-bone-ik\",", "").Replace("\"id\": \"reach\"", "\"id\": \"reach\", \"kind\": \"two-bone-ik\"");
        Assert.Equal("reach", CharacterModel.FromJson(reordered).Constraints.Single().Id);
        Assert.Throws<JsonException>(() => CharacterModel.FromJson(json.Replace("two-bone-ik", "unknown-solver")));
        Assert.Throws<InvalidDataException>(() => CharacterModel.FromJson(json.Replace("\"solver\": \"bone\"", "\"solver\": \"mystery\"")));
        foreach (var version in new[] { 2, 3 })
        {
            var old = TestModels.Creature().ToJson().Replace($"\"version\": {CharacterModel.CurrentVersion}", $"\"version\": {version}");
            var loaded = CharacterModel.FromJson(old);
            Assert.Equal(CharacterModel.CurrentVersion, loaded.Version); Assert.Empty(loaded.Constraints); Assert.Equal(2, loaded.Chains.Count);
        }
        var duplicate = TestModels.Creature(); duplicate.Constraints.Add(duplicate.Chains[0] with { });
        Assert.Contains("duplicate", Assert.Throws<InvalidDataException>(duplicate.Validate).Message);
    }

    [Fact]
    public void ResourceTemplatesCopyConstraintDefinitionsAndClipsIndependently()
    {
        var source = Machine(); var sourceClip = Aim(ResolvedModel.From(source), new(1, 1, .2f));
        var template = new ModelTemplate { Id = "linkage", Name = "Linkage", Model = source.Id, Animations = { ["aim"] = sourceClip.Id } };
        var copy = template.Instantiate("copy", "Copy", _ => source, _ => sourceClip);
        var pose = PoseEvaluator.Sample(ResolvedModel.From(copy.Model), copy.Animations.Single(), 0);
        TestModels.Near(new(1, 1, .2f), pose.World("tool"));
        copy.Model.IkChains.Single().Bend = -1;
        Assert.Equal(1, source.IkChains.Single().Bend); Assert.Empty(copy.Model.Chains); Assert.Single(copy.Model.Constraints);
    }

    [Fact]
    public void EditorCanBuildKeyPlantUndoAndReopenAnArbitraryBoneRig()
    {
        var root = Path.Combine(Path.GetTempPath(), "bone-constraint-" + Guid.NewGuid().ToString("N"));
        try
        {
            var session = new EditorSession(AuthoringWorkspace.Open(root));
            Assert.True(session.NewModel("linkage", "Linkage", "empty"), session.Message);
            var document = session.SubjectModel!;
            Assert.True(session.Edit(document, () =>
            {
                ModelAuthoring.AddBone(document.Asset, null, "beam");
                ModelAuthoring.AddBone(document.Asset, "beam", "hinge");
                ModelAuthoring.AddBone(document.Asset, "hinge", "tool");
                document.Asset.Groups.Add(new() { Id = "work", Targets = ["hinge", "tool"] });
                ModelAuthoring.AddBoneIk(document.Asset, "tool");
                ModelAuthoring.AddPartToBone(document.Asset, PuppetPartKinds.Box, "beam");
            }), session.Message);
            Assert.Equal(new[] { "tool-chain" }, document.Asset.Groups.Single().Targets);
            Assert.Empty(document.Asset.Chains); Assert.Single(document.Asset.Constraints);
            Assert.True(session.Save(document), session.Message);
            Assert.True(session.NewClip("linkage-move", "Move", 1), session.Message);
            var clip = session.ClipDocument!; var model = session.Assets.Resolve("linkage")!;
            Assert.True(session.Edit(clip, () => ClipAuthoring.Pose(model, clip.Asset, PoseEvaluator.Rest(model), 0, "tool", new(.5f, .5f, 0))), session.Message);
            var at = PoseEvaluator.Sample(model, clip.Asset, 0);
            Assert.True(session.Edit(clip, () => ClipAuthoring.Plant(model, clip.Asset, at, "tool-chain", 0, .5f)), session.Message);
            session.SaveAll(); Assert.False(session.MessageIsError, session.Message);
            var reopened = AuthoredCatalog.Load(root); Assert.Empty(reopened.Errors);
            var pose = PoseEvaluator.Sample(reopened.Resolve("linkage"), reopened.Animations["linkage-move"], .25);
            TestModels.Near(new(.5f, .5f, 0), pose.World("tool")); Assert.Single(pose.Contacts);
            session.SetMode(Workspace.Model);
            Assert.True(session.Edit(document, () => ModelAuthoring.RemoveChain(document.Asset, "tool-chain")), session.Message);
            Assert.Empty(document.Asset.Constraints);
            session.Undo(); Assert.Single(document.Asset.Constraints);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    private static CharacterModel Machine(bool affine = true)
    {
        var model = new CharacterModel
        {
            Id = "machine", Name = "Machine",
            Controls =
            [
                new() { Id = "carrier", Transform = affine ? new() : null },
                new() { Id = "beam", Parent = "carrier", Length = 1, Transform = affine ? new() : null },
                new() { Id = "hinge", Parent = "beam", Length = 1, Rest = new(1, 0, .1f), Transform = affine ? new() { X = 1 } : null },
                new() { Id = "tool", Parent = "hinge", Rest = new(2, 0, .2f), RestAngle = .3f, Transform = affine ? new() { X = 1, Rotation = .3f } : null },
                new() { Id = "flag", Parent = "beam", Rest = new(0, .25f), Transform = affine ? new() { Y = .25f } : null },
                new() { Id = "payload", Parent = "tool", Rest = PuppetPoint.From(new Vector3(new Vector2(2, 0) + Rotation2D.Apply(new(.2f, .3f), .3f), .3f)), RestAngle = .3f, Transform = affine ? new() { X = .2f, Y = .3f } : null }
            ],
            Constraints = [new ModelChain { Id = "reach", Root = "beam", Joint = "hinge", End = "tool", Solver = ModelChain.BoneSolver }],
            Sockets = [new() { Id = "socket", Control = "tool", OffsetX = .2f }]
        };
        ModelAuthoring.AddPartToBone(model, PuppetPartKinds.Box, "beam"); model.Validate(); return model;
    }

    private static MotionClip Aim(ResolvedModel model, Vector3 world)
    {
        var clip = ClipAuthoring.New(model, "machine-aim", "Aim");
        ClipAuthoring.SetKey(clip, new(MotionClip.TargetKind, "reach"), 0, world - model.Rest["tool"]);
        clip.Validate(model); return clip;
    }

    private static void AssertFramesAgree(EvaluatedPose pose)
    {
        foreach (var (id, frame) in pose.Bones)
        {
            Assert.InRange(Vector2.Distance(new(frame.M31, frame.M32), XY(pose.World(id))), 0, 1e-5f);
            Assert.Equal(MathF.Atan2(frame.M12, frame.M11), pose.Angles[id], 4);
        }
    }
    private static Vector2 XY(Vector3 v) => new(v.X, v.Y);
}
