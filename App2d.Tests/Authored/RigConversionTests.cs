using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Mathematics.Transforms;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>
/// P3b of the character-system migration: the Person left arm and the linkage fixture as canonical bone chains, the
/// declared constraint semantics on them, and the position-to-dimension adapter for the Person build rule.
/// </summary>
public sealed class RigConversionTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);

    private static Vector2 XY(Vector3 v) => new(v.X, v.Y);

    [Fact]
    public void TheShippedPersonLimbsAreBoneConstraintsWithTheShouldersAsPivots()
    {
        // P11a: every limb is a bone-solver constraint. The shoulders stay point pivots (they frame the grips and carry the
        // clips' turns); the solved upper arms hang beneath them; the hips are the leg roots; every end keeps the character axes.
        var person = Catalog.Models["person"];
        Assert.Equal(CharacterModel.CurrentVersion, person.Version);
        Assert.All(person.IkChains, c => Assert.Equal(ModelChain.BoneSolver, c.Solver));
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

    [Fact]
    public void ConvertingAPointRunToBoneNodesAndBackReproducesItsFile()
    {
        var person = Catalog.Models["person"];
        var points = CharacterModel.FromJson(person.ToJson());
        // The point form of the run needs the point solver (a bone constraint needs lengths); the bone form restores it.
        points.IkChains.Single(c => c.Id == Humanoid.LeftArm).Solver = ModelChain.PointSolver;
        var report = RigConversion.ToPointControls(points, [Humanoid.LeftUpperArmBone, Humanoid.LeftElbow, Humanoid.LeftHand]);
        // Three controls, and the arms parameter's two length bindings re-expressed as the elbow's and hand's local offsets.
        Assert.Equal(5, report.Count);
        Assert.Equal("arms", ResolvedModel.Rig(points).Parameters.BindingOfControl(Humanoid.LeftElbow, RigProperties.LocalX)!.Value.Parameter.Id);
        var back = CharacterModel.FromJson(points.ToJson());
        RigConversion.ToBoneChain(back, [Humanoid.LeftUpperArmBone, Humanoid.LeftElbow, Humanoid.LeftHand]);
        back.Controls.Single(c => c.Id == Humanoid.LeftHand).Link!.Orientation = RigLinkOrientations.Character;
        back.Controls.Single(c => c.Id == Humanoid.LeftHand).Local!.Rotation = 0;
        back.IkChains.Single(c => c.Id == Humanoid.LeftArm).Solver = ModelChain.BoneSolver;
        back.Validate();
        Assert.Equal(person.ToJson(), back.ToJson());
        Assert.Empty(ResolvedModel.From(points, null, Catalog.Parts.GetValueOrDefault).Rest.Where(r => Vector3.Distance(r.Value, Catalog.Resolve("person").Rest[r.Key]) > 1e-5f));
    }

    [Fact]
    public void ThePointAndBoneSolversAgreeOnPositionsSocketsAndContactsAcrossTheClips()
    {
        var person = Catalog.Models["person"];
        var bones = Catalog.Resolve("person");
        // The same rig on the retired point adapter's semantics: point controls solved by the position-only constraint.
        var points = CharacterModel.FromJson(person.ToJson());
        foreach (var chain in points.IkChains)
        {
            chain.Solver = ModelChain.PointSolver;
            RigConversion.ToPointControls(points, [chain.Root, chain.Joint, chain.End]);
        }
        points.Validate();
        var legacy = ResolvedModel.From(points, null, Catalog.Parts.GetValueOrDefault);
        foreach (var variant in new ModelVariant?[] { null, Catalog.Variants["tall-thin"], Catalog.Variants["brute"] })
        {
            var a = variant is null ? bones : ResolvedModel.From(person, variant, Catalog.Parts.GetValueOrDefault);
            var b = variant is null ? legacy : ResolvedModel.From(points, variant, Catalog.Parts.GetValueOrDefault);
            foreach (var (clipId, seconds) in new[] { ("person-walk", .3f), ("person-run", .2f), ("player-sword-side-cut", .06f), ("player-climb", .4f), ("person-hammer-slam", .5f) })
            {
                var clip = Catalog.Animations[clipId];
                var pa = PoseEvaluator.Sample(a, clip, seconds, true);
                var pb = PoseEvaluator.Sample(b, clip, seconds, true);
                foreach (var control in a.Order)
                {
                    TestModels.Near(pa.World(control.Id), pb.World(control.Id), 1e-5f, $"{variant?.Id} {clipId} {control.Id}");
                }
                foreach (var socket in person.Sockets)
                {
                    var sa = new ActorPose(pa, Vector2.Zero, 1).Socket(socket);
                    var sb = new ActorPose(pb, Vector2.Zero, 1).Socket(socket);
                    Assert.InRange(Vector3.Distance(sa.Origin, sb.Origin), 0, 1e-5f);
                    // A socket pointing at a joint turns by the point adapter's full-reach inset over the segment (baseline 7.2, P11a).
                    Assert.InRange(Vector3.Distance(sa.XAxis, sb.XAxis), 0, 1e-2f);
                }
                Assert.Equal(pa.Chains.Select(c => (c.Chain, c.Reached)), pb.Chains.Select(c => (c.Chain, c.Reached)));
                // The solved frames point along the limb; the pivot keeps the authored turn on both.
                Assert.Equal(pa.Angles[Humanoid.RightShoulder], pb.Angles[Humanoid.RightShoulder], 5);
                Assert.NotEqual(pa.Angles[Humanoid.LeftElbow], pb.Angles[Humanoid.LeftElbow]);
            }
        }
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
    public void TheLinkageIsBoneChainsWhoseRodKeepsItsDeclaredPointSolver()
    {
        var linkage = TestModels.Linkage();
        Assert.All(new[] { "hub", "crank-pin", "piston", "rod-elbow", "rod-end" }, id => Assert.Equal(RigNodeKinds.Bone, linkage.Controls.Single(c => c.Id == id).Node));
        Assert.Equal(RigNodeKinds.Point, linkage.Controls.Single(c => c.Id == "frame").Node);
        Assert.Equal(ModelChain.PointSolver, linkage.IkChains.Single().Solver);
        var model = ResolvedModel.From(linkage);
        var points = ResolvedModel.From(TestModels.LinkagePoints());
        var crank = TestModels.LinkageCrank(model);
        foreach (var seconds in new[] { 0, .125, .5, .875 })
        {
            var a = PoseEvaluator.Sample(model, crank, seconds, true);
            var b = PoseEvaluator.Sample(points, crank, seconds, true);
            foreach (var control in model.Order)
            {
                TestModels.Near(a.World(control.Id), b.World(control.Id), 1e-5f, control.Id);
            }
            Assert.Equal(a.Chains.Single().Reached, b.Chains.Single().Reached);
            Assert.Equal(a.Chains.Single().Residual, b.Chains.Single().Residual, 5);
        }
        // Changing the hub's length moves its bound pin; the rod re-solves to the new pin without stretching.
        var longer = TestModels.Linkage();
        longer.Controls.Single(c => c.Id == "hub").Length = .6f;
        var reach = ResolvedModel.From(longer);
        TestModels.Near(new(.6f, 1, 0), reach.Rest["crank-pin"]);
        Assert.Equal(model.Length("piston", "rod-elbow"), reach.Length("piston", "rod-elbow"), 5);
    }

    [Fact]
    public void TheRigidLinkageTurnsItsRodFramesAndCarriesAttachmentsWithThem()
    {
        var model = ResolvedModel.From(TestModels.LinkageRigid());
        Assert.Equal(ModelChain.BoneSolver, model.IkConstraints.Single().Solver);
        var crank = TestModels.LinkageCrank(model);
        var rest = PoseEvaluator.Rest(model);
        var turned = PoseEvaluator.Sample(model, crank, .125, true);
        // P4b: the pin at 1/8 turn is 5.6e-3 past full reach; the rigid solve reports that gap instead of claiming the target.
        Assert.False(turned.Chains.Single().Reached);
        Assert.InRange(turned.Chains.Single().Residual, .005f, .006f);
        Assert.True(PoseEvaluator.Sample(model, crank, 0, true).Chains.Single().Reached);
        TestModels.Near(turned.World("crank-pin"), turned.World("rod-end"), 2e-2f, "the rod follows the pin");
        // Rigid frames: the piston and elbow frames point along their segments after the solve, and the point adapter would not have turned them.
        var piston = XY(turned.World("rod-elbow")) - XY(turned.World("piston"));
        Assert.Equal(MathF.Atan2(piston.Y, piston.X), MathF.IEEERemainder(turned.Angles["piston"], MathF.Tau), 3);
        Assert.NotEqual(rest.Angles["piston"], turned.Angles["piston"], 3);
        var part = model.Parts.Single(p => p.Id == "rod-upper");
        var art = PartAttachment2D.FrameOf(part, model, turned);
        Assert.InRange(Vector2.Distance(Vector2.Normalize(art.YAxis), Vector2.Normalize(piston)), 0, 1e-4f);
        // The segment lengths are the declared dimensions, scaled by nothing.
        Assert.Equal(model.Nodes["piston"].Length, Vector2.Distance(XY(turned.World("piston")), XY(turned.World("rod-elbow"))), 4);
        Assert.Equal(model.Nodes["rod-elbow"].Length, Vector2.Distance(XY(turned.World("rod-elbow")), XY(turned.World("rod-end"))), 4);
    }

    [Fact]
    public void ConversionRefusesWhatItCannotRepresentByName()
    {
        var model = TestModels.LinkagePoints();
        Assert.Contains("not a child", Assert.Throws<InvalidOperationException>(() => RigConversion.ToBoneChain(model, ["hub", "piston"])).Message);
        Assert.Contains("at least two", Assert.Throws<InvalidOperationException>(() => RigConversion.ToBoneChain(model, ["hub"])).Message);
        model.Controls.Single(c => c.Id == "hub").RestAngle = .2f;
        Assert.Contains("rest angle", Assert.Throws<InvalidOperationException>(() => RigConversion.ToBoneChain(model, ["hub", "crank-pin"])).Message);
        var (wheel, _) = TestModels.Wheel();
        Assert.Contains("affine", Assert.Throws<InvalidOperationException>(() => RigConversion.ToBoneChain(wheel, ["wheel-root", "spoke-tip"])).Message);
        var bones = TestModels.Linkage();
        Assert.Contains("linked child", Assert.Throws<InvalidOperationException>(() => RigConversion.ToPointControls(bones, ["hub"])).Message);
        Assert.Contains("only bone nodes", Assert.Throws<InvalidOperationException>(() => RigConversion.ToPointControls(bones, ["frame"])).Message);
    }
}
