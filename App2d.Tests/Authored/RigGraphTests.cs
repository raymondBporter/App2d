using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json.Nodes;

namespace App2d.Tests.Authored;

/// <summary>P3a of the character-system migration: the canonical parent-local rig graph and its one evaluated frame per node.</summary>
public sealed class RigGraphTests
{
    /// <summary>A crank on a hub: the pin is bound to the hub's tip, a lamp sits at the hub's midpoint, a flag keeps the character's axes.</summary>
    private static CharacterModel Crank(float hubLength = .4f, float scale = 1, bool reflect = false) => new()
    {
        Id = "crank",
        Name = "Crank",
        Controls =
        [
            new() { Id = "frame", Rest = new(0, 1, .1f) },
            new() { Id = "hub", Parent = "frame", Node = RigNodeKinds.Bone, Local = new() { X = .5f, Y = .2f, Rotation = .3f, Scale = scale, Reflect = reflect, Layer = -.05f }, Length = hubLength },
            new() { Id = "pin", Parent = "hub", Node = RigNodeKinds.Bone, Link = new(), Local = new() { Rotation = .2f, Layer = -.01f }, Length = .25f },
            new() { Id = "lamp", Parent = "hub", Node = RigNodeKinds.Bone, Link = new() { T = .5f, OffsetY = .1f } },
            new() { Id = "flag", Parent = "hub", Node = RigNodeKinds.Bone, Link = new() { T = 1, Orientation = RigLinkOrientations.Character }, Local = new() { Rotation = .1f } },
            new() { Id = "tip", Parent = "pin", Node = RigNodeKinds.Bone, Link = new() },
        ],
        Parts = [new() { Id = "arm", A = "hub", Frame = "hub", OffsetX = hubLength / 2, Geometry = RoundedRectangle2D.Builder.FromSize(hubLength, .1f, .02f), Material = new() { Fill = "#fff8e7", Outline = new() } }],
    };

    private static MotionClip Turn(ResolvedModel model, float hubTurn, float pinTurn = 0)
    {
        var clip = ClipAuthoring.New(model, "crank-turn", "Turn");
        ClipAuthoring.SetKey(clip, new(MotionClip.RotateKind, "hub"), 0, default, hubTurn);
        ClipAuthoring.SetKey(clip, new(MotionClip.RotateKind, "pin"), 0, default, pinTurn);
        clip.Validate(model);
        return clip;
    }

    private static Vector2 XY(Vector3 v) => new(v.X, v.Y);

    [Fact]
    public void RestFramesComposeLocalSimilaritiesAndLinksDeriveFromTheParentLength()
    {
        var model = ResolvedModel.From(Crank());
        var hub = model.Nodes["hub"];
        Assert.Equal(RigNodeKinds.Bone, hub.Kind);
        TestModels.Near(new(.5f, 1.2f, .05f), model.Rest["hub"]);
        Assert.Equal(.3f, hub.RestAngle, 5);
        Assert.Equal(-.05f, model.LayerOffsets["hub"], 6);
        var expectedPin = new Vector2(.5f, 1.2f) + Rotation2D.Apply(new(.4f, 0), .3f);
        TestModels.Near(new(expectedPin, .04f), model.Rest["pin"]);
        Assert.Equal(.5f, model.Nodes["pin"].RestAngle, 5);
        TestModels.Near(new(new Vector2(.5f, 1.2f) + Rotation2D.Apply(new(.2f, .1f), .3f), .05f), model.Rest["lamp"]);
        // The point model's derived rest reproduces its authored character-frame values exactly.
        var point = ResolvedModel.From(TestModels.Creature());
        foreach (var control in point.Base.Controls)
        {
            Assert.Equal(control.Rest.XYZ, point.Rest[control.Id]);
            Assert.Equal(RigNodeKinds.Point, point.Nodes[control.Id].Kind);
        }
    }

    [Fact]
    public void RotatingAParentCarriesLinkedChildrenAndAttachmentsAndAnglesAccumulate()
    {
        var model = ResolvedModel.From(Crank());
        var pose = PoseEvaluator.Sample(model, Turn(model, MathF.Tau + .5f, -.2f), 0);
        var hubAngle = .3f + MathF.Tau + .5f;
        Assert.Equal(hubAngle, pose.Angles["hub"], 4);
        Assert.Equal(hubAngle + .2f - .2f, pose.Angles["pin"], 4);
        var hubPoint = XY(pose.World("hub"));
        TestModels.Near(new(hubPoint + Rotation2D.Apply(new(.4f, 0), hubAngle), .04f), pose.World("pin"));
        TestModels.Near(new(hubPoint + Rotation2D.Apply(new(.2f, .1f), hubAngle), .05f), pose.World("lamp"));
        TestModels.Near(new(XY(pose.World("pin")) + Rotation2D.Apply(new(.25f, 0), pose.Angles["pin"]), .04f), pose.World("tip"));
        var art = PartAttachment2D.FrameOf(model.Parts[0], model, pose);
        TestModels.Near(new(hubPoint + Rotation2D.Apply(new(.2f, 0), hubAngle), .05f), art.Origin);
        Assert.InRange(Vector2.Distance(art.XAxis, Rotation2D.Apply(Vector2.UnitX, hubAngle)), 0, 1e-4f);
        // Positions, frames and angles agree after evaluation: no consumer reconstructs a different pose.
        foreach (var (id, frame) in pose.Bones)
        {
            Assert.Equal(new Vector2(frame.M31, frame.M32), XY(pose.World(id)));
            Assert.InRange(MathF.Abs(MathF.IEEERemainder(MathF.Atan2(frame.M12, frame.M11) - pose.Angles[id], MathF.Tau)), 0, 1e-4f);
        }
    }

    [Fact]
    public void ChangingALengthMovesBoundChildrenWithoutStretchingTheirFrames()
    {
        var shortArm = ResolvedModel.From(Crank(.4f));
        var longArm = ResolvedModel.From(Crank(.8f));
        var hub = XY(shortArm.Rest["hub"]);
        TestModels.Near(new(hub + Rotation2D.Apply(new(.8f, 0), .3f), .04f), longArm.Rest["pin"]);
        TestModels.Near(new(hub + Rotation2D.Apply(new(.4f, .1f), .3f), .05f), longArm.Rest["lamp"]);
        foreach (var id in new[] { "pin", "lamp", "tip" })
        {
            var a = new Frame2D(shortArm.RestTransforms[id]);
            var b = new Frame2D(longArm.RestTransforms[id]);
            Assert.InRange(Vector2.Distance(a.XAxis, b.XAxis), 0, 1e-5f);
            Assert.InRange(Vector2.Distance(a.YAxis, b.YAxis), 0, 1e-5f);
            Assert.Equal(1, b.XAxis.Length(), 5);
        }
        // The pin's own length is unchanged, so the tip stays a pin length beyond it.
        Assert.Equal(.25f, Vector2.Distance(XY(longArm.Rest["pin"]), XY(longArm.Rest["tip"])), 5);
    }

    [Theory, InlineData(2, false), InlineData(.5f, true), InlineData(1, true)]
    public void UniformScaleAndReflectionStaySimilaritiesAndReverseChildTurns(float scale, bool reflect)
    {
        var model = ResolvedModel.From(Crank(scale: scale, reflect: reflect));
        var hand = reflect ? -1 : 1;
        var pose = PoseEvaluator.Sample(model, Turn(model, .4f, .25f), 0);
        var hubAngle = .3f + .4f;
        Assert.Equal(hubAngle, pose.Angles["hub"], 4);
        Assert.Equal(hubAngle + hand * (.2f + .25f), pose.Angles["pin"], 4);
        var hubFrame = new Frame2D(pose.Bones["hub"]);
        Assert.Equal(scale, hubFrame.XAxis.Length(), 4);
        Assert.Equal(scale, hubFrame.YAxis.Length(), 4);
        Assert.Equal(0, Vector2.Dot(hubFrame.XAxis, hubFrame.YAxis), 4);
        Assert.Equal(hand, Math.Sign(pose.Bones["hub"].GetDeterminant()));
        Assert.Equal(hand, Math.Sign(pose.Bones["tip"].GetDeterminant()));
        Assert.Equal(hand, model.Nodes["pin"].Handedness);
        // The bound origins scale with the parent, but the children's rotating frames keep the parent's uniform scale only.
        TestModels.Near(new(XY(pose.World("hub")) + Rotation2D.Apply(new(.4f * scale, 0), hubAngle), .04f), pose.World("pin"));
        Assert.Equal(scale, new Frame2D(pose.Bones["pin"]).XAxis.Length(), 4);
        TestModels.Near(new(XY(pose.World("pin")) + Rotation2D.Apply(new(.25f * scale, 0), pose.Angles["pin"]), .04f), pose.World("tip"));
    }

    [Fact]
    public void ACharacterOrientedLinkFollowsTheOriginButKeepsTheCharacterAxes()
    {
        var model = ResolvedModel.From(Crank(scale: 2, reflect: true));
        var pose = PoseEvaluator.Sample(model, Turn(model, .7f), 0);
        TestModels.Near(pose.World("pin") with { Z = pose.World("hub").Z }, pose.World("flag"));
        Assert.Equal(.1f, pose.Angles["flag"], 5);
        var frame = new Frame2D(pose.Bones["flag"]);
        Assert.Equal(1, frame.XAxis.Length(), 5);
        Assert.Equal(1, Math.Sign(pose.Bones["flag"].GetDeterminant()));
        Assert.InRange(Vector2.Distance(frame.XAxis, Rotation2D.Apply(Vector2.UnitX, .1f)), 0, 1e-5f);
    }

    [Fact]
    public void APointRigAndItsCanonicalConversionEvaluateAlike()
    {
        // The Creature's arm as bone nodes: shoulder bone toward the elbow, elbow bound to its tip, hand bound to the elbow's tip.
        var point = TestModels.Creature();
        var converted = CharacterModel.FromJson(point.ToJson());
        var shoulder = converted.Controls.Single(c => c.Id == "shoulder");
        var elbow = converted.Controls.Single(c => c.Id == "elbow");
        var hand = converted.Controls.Single(c => c.Id == "hand");
        var toElbow = elbow.Rest.XY - shoulder.Rest.XY;
        var toHand = hand.Rest.XY - elbow.Rest.XY;
        var body = converted.Controls.Single(c => c.Id == "body");
        shoulder.Node = RigNodeKinds.Bone;
        shoulder.Local = new() { X = shoulder.Rest.X - body.Rest.X, Y = shoulder.Rest.Y - body.Rest.Y, Rotation = MathF.Atan2(toElbow.Y, toElbow.X), Layer = shoulder.Rest.Z - body.Rest.Z };
        shoulder.Length = toElbow.Length();
        shoulder.Rest = default;
        elbow.Node = RigNodeKinds.Bone;
        elbow.Link = new();
        elbow.Local = new() { Rotation = MathF.Atan2(toHand.Y, toHand.X) - MathF.Atan2(toElbow.Y, toElbow.X) };
        elbow.Length = toHand.Length();
        elbow.Rest = default;
        hand.Node = RigNodeKinds.Bone;
        hand.Link = new();
        hand.Rest = default;
        var a = ResolvedModel.From(point);
        var b = ResolvedModel.From(CharacterModel.FromJson(converted.ToJson()));
        var clip = TestModels.Clip(a);
        clip.Tracks.Add(new() { Kind = MotionClip.RotateKind, Target = "body", Keys = [new() { Angle = .4f }] });
        clip.Tracks.Add(new() { Target = "shoulder", Keys = [new() { Value = new(.05f, -.1f, .02f) }] });
        clip.Tracks.Add(new() { Kind = MotionClip.TargetKind, Target = "arm", Keys = [new() { Value = new(-.2f, .3f, 0) }] });
        clip.Validate(b);
        foreach (var time in new[] { 0, .4 })
        {
            var pa = PoseEvaluator.Sample(a, clip, time);
            var pb = PoseEvaluator.Sample(b, clip, time);
            foreach (var control in a.Order)
            {
                TestModels.Near(pa.World(control.Id), pb.World(control.Id), 1e-5f, control.Id);
            }
            // The converted bones carry the limb's direction in their frames; the point rig keeps its rest-plus-turn angles.
            Assert.Equal(pa.Angles["body"], pb.Angles["body"], 5);
            Assert.Equal(pa.Chains.Single(c => c.Chain == "arm").Residual, pb.Chains.Single(c => c.Chain == "arm").Residual, 5);
        }
    }

    [Fact]
    public void BoneNodesRoundTripAtTheCurrentVersionAndOlderFilesReadThroughTheLegacyReader()
    {
        var model = Crank(scale: 1.5f, reflect: true);
        model.Validate();
        var json = model.ToJson();
        Assert.Contains("\"node\": \"bone\"", json);
        Assert.Contains("\"link\"", json);
        Assert.Contains("\"reflect\": true", json);
        Assert.DoesNotContain("\"rest\"", json.Split("\"id\": \"hub\"")[1].Split("\"id\": \"lamp\"")[0]);
        Assert.Equal(json, CharacterModel.FromJson(json).ToJson());
        Assert.Equal(CharacterModel.CurrentVersion, CharacterModel.FromJson(json).Version);

        // A version-6 file: point chains in their own list, affine controls recognized by their transform.
        var (wheel, _) = TestModels.Wheel();
        var legacy = JsonNode.Parse(wheel.ToJson())!.AsObject();
        legacy["version"] = 6;
        foreach (var control in legacy["controls"]!.AsArray())
        {
            control!.AsObject().Remove("node");
        }
        var read = CharacterModel.FromJson(legacy.ToJsonString());
        Assert.All(read.Controls, c => Assert.Equal(RigNodeKinds.Affine, c.Node));
        Assert.Equal(wheel.ToJson(), read.ToJson());
        var creature = JsonNode.Parse(TestModels.Creature().ToJson())!.AsObject();
        creature["version"] = 5;
        var chains = creature["constraints"]!.AsArray();
        creature.Remove("constraints");
        creature["chains"] = chains;
        Assert.Equal(["arm", "leg"], CharacterModel.FromJson(creature.ToJsonString()).IkChains.Select(c => c.Id));
        Assert.Contains("unsupported format/version", Assert.Throws<InvalidDataException>(() => CharacterModel.FromJson(TestModels.Creature().ToJson().Replace("\"version\": 7", "\"version\": 4"))).Message);
    }

    [Theory]
    [InlineData("rest-on-bone", "derives its rest")]
    [InlineData("affine-without-transform", "needs its transform")]
    [InlineData("linked-translation", "linked origin")]
    [InlineData("unknown-node", "unknown node kind")]
    [InlineData("root-link", "needs a parent")]
    [InlineData("point-with-transform", "declare node")]
    [InlineData("zero-scale", "scale")]
    public void DeclaredSemanticsAreValidatedByName(string edit, string fragment)
    {
        var model = Crank();
        var hub = model.Controls.Single(c => c.Id == "hub");
        switch (edit)
        {
            case "rest-on-bone":
                hub.Rest = new(1, 2);
                break;
            case "affine-without-transform":
                hub.Node = RigNodeKinds.Affine;
                break;
            case "linked-translation":
                model.Controls.Single(c => c.Id == "pin").Local!.X = .1f;
                break;
            case "unknown-node":
                hub.Node = "spline";
                break;
            case "root-link":
                model.Controls.Single(c => c.Id == "frame").Node = RigNodeKinds.Bone;
                model.Controls.Single(c => c.Id == "frame").Rest = default;
                model.Controls.Single(c => c.Id == "frame").Link = new();
                break;
            case "point-with-transform":
                model.Controls.Single(c => c.Id == "frame").Transform = new();
                break;
            case "zero-scale":
                hub.Local!.Scale = 0;
                break;
        }
        Assert.Contains(fragment, Assert.Throws<InvalidDataException>(model.Validate).Message);
    }

    [Fact]
    public void PointIkOnBoneNodesNeedsTipBoundJointsOrABoneConstraint()
    {
        var model = Crank();
        var pin = model.Controls.Single(c => c.Id == "pin");
        var tip = model.Controls.Single(c => c.Id == "tip");
        model.Constraints.Add(new ModelChain { Id = "reach", Root = "hub", Joint = "pin", End = "tip" });
        model.Validate();
        var resolved = ResolvedModel.From(model);
        var clip = ClipAuthoring.New(resolved, "crank-reach", "Reach");
        ClipAuthoring.SetKey(clip, new(MotionClip.TargetKind, "reach"), 0, new(-.3f, .2f, 0));
        clip.Validate(resolved);
        var pose = PoseEvaluator.Sample(resolved, clip, 0);
        // The legacy adapter moves the joint and end and keeps every frame's setup orientation.
        Assert.Equal(.4f, Vector2.Distance(XY(pose.World("hub")), XY(pose.World("pin"))), 4);
        Assert.Equal(.25f, Vector2.Distance(XY(pose.World("pin")), XY(pose.World("tip"))), 4);
        Assert.Equal(resolved.Nodes["pin"].RestAngle, pose.Angles["pin"], 5);
        pin.Link = new() { T = .9f };
        Assert.Contains("point solver would ignore", Assert.Throws<InvalidDataException>(model.Validate).Message);
        pin.Link = new();
        tip.Length = .1f;
        Assert.Contains("point solver would ignore", Assert.Throws<InvalidDataException>(model.Validate).Message);
    }
}
