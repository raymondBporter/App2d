using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>
/// The Person's limbs and the linkage fixture as bone constraints (P3b, P11a), and the authoring conversion of a point run
/// into one (<see cref="RigConversion.ToBoneConstraint"/>, the one way a point run gets a constraint since P11c).
/// </summary>
public sealed class RigConversionTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);

    private static Vector2 XY(Vector3 v) => new(v.X, v.Y);

    [Fact]
    public void TheShippedPersonLimbsAreBoneConstraintsWithTheShouldersAsPivots()
    {
        // P11a: every limb is a bone constraint. The shoulders stay point pivots (they frame the grips and carry the
        // clips' turns); the solved upper arms hang beneath them; the hips are the leg roots; every end keeps the character axes.
        var person = Catalog.Models["person"];
        Assert.Equal(CharacterModel.CurrentVersion, person.Version);
        Assert.Equal([Humanoid.LeftUpperArmBone, Humanoid.LeftHip, Humanoid.RightUpperArmBone, Humanoid.RightHip], person.IkChains.Select(c => c.Root));
        Assert.Equal([Humanoid.LeftShoulder, CharacterModel.Locomotion, Humanoid.RightShoulder, CharacterModel.Locomotion], person.IkChains.Select(c => c.Frame));
        foreach (var shoulder in new[] { Humanoid.LeftShoulder, Humanoid.RightShoulder })
        {
            Assert.Equal(RigNodeKinds.Point, person.Controls.Single(c => c.Id == shoulder).Node);
        }
        foreach (var chain in person.IkChains)
        {
            var rootNode = person.Controls.Single(c => c.Id == chain.Root);
            var joint = person.Controls.Single(c => c.Id == chain.Joint);
            var end = person.Controls.Single(c => c.Id == chain.End);
            Assert.Equal(RigNodeKinds.Bone, rootNode.Node);
            Assert.Null(rootNode.Link);
            Assert.True(rootNode.Length > .3f && joint.Length > .3f);
            Assert.True(joint.Link is { IsTip: true });
            Assert.Equal(RigLinkOrientations.Character, end.Link?.Orientation);
            Assert.Equal(0, end.Length);
        }
        var resolved = Catalog.Resolve("person");
        var rest = PoseEvaluator.Rest(resolved);
        // The pivot and its bone share an origin; the end keeps the character axes at rest and under the walk.
        TestModels.Near(rest.Points[Humanoid.LeftShoulder], rest.Points[Humanoid.LeftUpperArmBone], 1e-6f);
        var walking = PoseEvaluator.Sample(resolved, Catalog.Animations["person-walk"], .3);
        Assert.Equal(0, walking.Angles[Humanoid.LeftHand], 5);
        Assert.Equal(0, walking.Angles[Humanoid.LeftFoot], 5);
        Assert.NotEqual(0, walking.Angles[Humanoid.LeftUpperArmBone]);
        Assert.All(walking.Chains, c => Assert.True(c.Reached));
    }

    private static MotionClip CreatureClip(ResolvedModel creature)
    {
        // The arm reaches out while the shoulder turns (the clip keys the chain root's rotation); the leg plants.
        var clip = ClipAuthoring.New(creature, "creature-reach", "Reach", 1, loop: false);
        clip.Tracks.Add(new() { Kind = MotionClip.RotateKind, Target = "shoulder", Keys = [new(), new() { Time = 1, Angle = .6f }] });
        clip.Tracks.Add(new() { Kind = MotionClip.TargetKind, Target = "arm", Keys = [new(), new() { Time = .5f, Value = new(-.2f, -.3f, 0) }, new() { Time = 1, Value = new(.05f, .1f, 0) }] });
        clip.Tracks.Add(new() { Kind = MotionClip.TargetKind, Target = "leg", Keys = [new(), new() { Time = 1, Value = new(.15f, .1f, 0) }] });
        clip.Tracks.Add(new() { Kind = MotionClip.TranslateKind, Target = "body", Keys = [new(), new() { Time = 1, Value = new(0, .05f, 0) }] });
        clip.Validate(creature);
        return clip;
    }

    [Fact]
    public void APointRunConvertsIntoABoneConstraintWithAPivotWhereTheRootIsReadAndKeepsEveryRestSocketAndContact()
    {
        // The creature fixture before its conversion: point controls with position-only constraints over them.
        var source = new CharacterModel
        {
            Id = "creature",
            Name = "Creature",
            Controls =
            [
                new() { Id = "body", Rest = new(0, 1), Scale = "leg" },
                new() { Id = "shoulder", Parent = "body", Rest = new(0, 1.5f) },
                new() { Id = "elbow", Parent = "shoulder", Rest = new(.4f, 1.3f) },
                new() { Id = "hand", Parent = "elbow", Rest = new(.8f, 1.5f) },
                new() { Id = "hip", Parent = "body", Rest = new(0, 1) },
                new() { Id = "knee", Parent = "hip", Rest = new(.1f, .5f) },
                new() { Id = "foot", Parent = "knee", Rest = new(0, 0) },
            ],
            Constraints =
            [
                new ModelChain { Id = "arm", Root = "shoulder", Joint = "elbow", End = "hand", Bend = -1, Frame = "shoulder", Scale = "arm" },
                new ModelChain { Id = "leg", Root = "hip", Joint = "knee", End = "foot", Bend = 1, Scale = "leg" },
            ],
            Measures = [new() { Id = "leg", Path = ["hip", "knee", "foot"] }, new() { Id = "arm", Path = ["shoulder", "elbow", "hand"] }],
            Sockets = [new() { Id = "grip", Control = "hand", Frame = "shoulder" }, new() { Id = "shoe", Control = "foot", Toward = "knee" }],
        };
        var rests = source.Controls.ToDictionary(c => c.Id, c => c.Rest.XYZ);
        var arm = RigConversion.ToBoneConstraint(source, "arm", [], "upper-arm-bone");
        var leg = RigConversion.ToBoneConstraint(source, "leg", []);
        // The arm's root frames its own target and the grip: the shoulder stays the pivot, the solved bone hangs beneath it; the hip is its own root.
        Assert.Equal(("upper-arm-bone", "shoulder"), (arm.Root, arm.Pivot));
        Assert.Equal(("hip", null), (leg.Root, leg.Pivot));
        Assert.Equal("shoulder", source.IkChains.Single(c => c.Id == "arm").Frame);
        Assert.Equal(RigNodeKinds.Point, source.Controls.Single(c => c.Id == "shoulder").Node);
        Assert.Equal("shoulder", source.Controls.Single(c => c.Id == "upper-arm-bone").Parent);
        Assert.Equal(RigLinkOrientations.Character, source.Controls.Single(c => c.Id == "hand").Link!.Orientation);
        Assert.Equal(RigLinkOrientations.Character, source.Controls.Single(c => c.Id == "foot").Link!.Orientation);
        Assert.Contains(arm.Report, line => line.Contains("'shoulder' keeps its authored frame as the pivot") && line.Contains("its target frame") && line.Contains("socket 'grip' framed on it"));
        // Every rest position is where it was; the model round-trips; a second conversion is refused.
        var model = ResolvedModel.From(source);
        foreach (var (id, rest) in rests)
        {
            TestModels.Near(rest, model.Rest[id], 1e-6f, id);
        }
        Assert.Equal(source.ToJson(), CharacterModel.FromJson(source.ToJson()).ToJson());
        Assert.Contains("already runs over bone nodes", Assert.Throws<InvalidOperationException>(() => RigConversion.ToBoneConstraint(source, "arm", [])).Message);
        // Under a clip: the pivot keeps the keyed turn, the ends keep the character axes, the socket framed on the pivot follows it, both contacts reach.
        var clip = CreatureClip(model);
        foreach (var seconds in new[] { 0, .5, 1 })
        {
            var pose = PoseEvaluator.Sample(model, clip, seconds);
            Assert.Equal(.6f * seconds, pose.Angles["shoulder"], 5);
            Assert.Equal(0, pose.Angles["hand"], 5);
            Assert.Equal(0, pose.Angles["foot"], 5);
            Assert.NotEqual(pose.Angles["upper-arm-bone"], pose.Angles["shoulder"]);
            var grip = new ActorPose(pose, Vector2.Zero, 1).Socket(source.Sockets[0]);
            TestModels.Near(new(MathF.Cos(.6f * (float)seconds), MathF.Sin(.6f * (float)seconds), 0), grip.XAxis, 1e-5f, "grip axis follows the pivot's turn");
            Assert.All(pose.Chains, c => Assert.True(c.Reached));
        }
        // The fixture every other test shares is exactly this conversion.
        var fixture = TestModels.Creature();
        fixture.Sockets = source.Sockets;
        fixture.Parts.Clear();
        Assert.Equal(source.ToJson(), fixture.ToJson());
    }

    [Fact]
    public void ThePersonArmsParameterScalesTheBoneChainsLengths()
    {
        var person = Catalog.Models["person"];
        var tall = ResolvedModel.From(person, Catalog.Variants["tall-thin"], Catalog.Parts.GetValueOrDefault);
        var standard = Catalog.Resolve("person");
        var ratio = tall.Nodes[Humanoid.LeftUpperArmBone].Length / standard.Nodes[Humanoid.LeftUpperArmBone].Length;
        Assert.Equal(PersonBuild.TallThin.Arms, ratio, 4);
        Assert.Equal(PersonBuild.TallThin.Arms, tall.Nodes["left-elbow"].Length / standard.Nodes["left-elbow"].Length, 4);
        Assert.Equal(tall.Length("left-shoulder", "left-elbow"), tall.Nodes[Humanoid.LeftUpperArmBone].Length, 5);
        Assert.Equal(standard.Nodes[Humanoid.LeftUpperArmBone].Rotation, tall.Nodes[Humanoid.LeftUpperArmBone].Rotation, 4);
        // The same arms value on the mirrored right arm scales the same way.
        Assert.Equal(ratio, tall.Length("right-shoulder", "right-elbow") / standard.Length("right-shoulder", "right-elbow"), 4);
        // A tip-bound node's origin is not a variant property: it moves through the parent's length or its link (P6a).
        var frozen = new ModelVariant { Id = "frozen", Name = "Frozen", Base = "person" };
        Assert.Contains("bound to", Assert.Throws<InvalidOperationException>(() => ModelAuthoring.MoveRest(standard, frozen, "left-elbow", new(0, 1, 0), true)).Message);
        Assert.Empty(frozen.Overrides);
    }

    [Fact]
    public void TheLinkageIsBoneChainsWhoseRodTurnsItsFramesAndCarriesAttachmentsWithThem()
    {
        var linkage = TestModels.Linkage();
        Assert.All(new[] { "hub", "crank-pin", "piston", "rod-elbow", "rod-end" }, id => Assert.Equal(RigNodeKinds.Bone, linkage.Controls.Single(c => c.Id == id).Node));
        Assert.Equal(RigNodeKinds.Point, linkage.Controls.Single(c => c.Id == "frame").Node);
        var model = ResolvedModel.From(linkage);
        var crank = TestModels.LinkageCrank(model);
        var rest = PoseEvaluator.Rest(model);
        var turned = PoseEvaluator.Sample(model, crank, .125, true);
        // P4b: the pin at 1/8 turn is 5.6e-3 past full reach; the rigid solve reports that gap instead of claiming the target.
        Assert.False(turned.Chains.Single().Reached);
        Assert.InRange(turned.Chains.Single().Residual, .005f, .006f);
        Assert.True(PoseEvaluator.Sample(model, crank, 0, true).Chains.Single().Reached);
        TestModels.Near(turned.World("crank-pin"), turned.World("rod-end"), 2e-2f, "the rod follows the pin");
        // Rigid frames: the piston and elbow frames point along their segments after the solve.
        var piston = XY(turned.World("rod-elbow")) - XY(turned.World("piston"));
        Assert.Equal(MathF.Atan2(piston.Y, piston.X), MathF.IEEERemainder(turned.Angles["piston"], MathF.Tau), 3);
        Assert.NotEqual(rest.Angles["piston"], turned.Angles["piston"], 3);
        var part = model.Parts.Single(p => p.Id == "rod-upper");
        var art = PartAttachment2D.FrameOf(part, model, turned);
        Assert.InRange(Vector2.Distance(Vector2.Normalize(art.YAxis), Vector2.Normalize(piston)), 0, 1e-4f);
        // The segment lengths are the declared dimensions, scaled by nothing.
        Assert.Equal(model.Nodes["piston"].Length, Vector2.Distance(XY(turned.World("piston")), XY(turned.World("rod-elbow"))), 4);
        Assert.Equal(model.Nodes["rod-elbow"].Length, Vector2.Distance(XY(turned.World("rod-elbow")), XY(turned.World("rod-end"))), 4);
        // Changing the hub's length moves its bound pin; the rod re-solves to the new pin without stretching.
        var longer = TestModels.Linkage();
        longer.Controls.Single(c => c.Id == "hub").Length = .6f;
        var reach = ResolvedModel.From(longer);
        TestModels.Near(new(.6f, 1, 0), reach.Rest["crank-pin"]);
        Assert.Equal(model.Length("piston", "rod-elbow"), reach.Length("piston", "rod-elbow"), 5);
    }

    [Fact]
    public void ConversionRefusesWhatItCannotRepresentByName()
    {
        var model = TestModels.LinkagePoints();
        Assert.Contains("not a child", Assert.Throws<InvalidOperationException>(() => RigConversion.ToBoneChain(model, ["hub", "piston"])).Message);
        Assert.Contains("at least two", Assert.Throws<InvalidOperationException>(() => RigConversion.ToBoneChain(model, ["hub"])).Message);
        model.Controls.Single(c => c.Id == "hub").RestAngle = .2f;
        Assert.Contains("rest angle", Assert.Throws<InvalidOperationException>(() => RigConversion.ToBoneChain(model, ["hub", "crank-pin"])).Message);
    }
}
