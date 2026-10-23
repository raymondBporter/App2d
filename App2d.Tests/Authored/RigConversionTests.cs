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
    public void TheShippedPersonLeftArmIsABoneChainAndConvertsBackWithoutMovingAnything()
    {
        var person = Catalog.Models["person"];
        Assert.Equal(CharacterModel.CurrentVersion, person.Version);
        var shoulder = person.Controls.Single(c => c.Id == "left-shoulder");
        var elbow = person.Controls.Single(c => c.Id == "left-elbow");
        var hand = person.Controls.Single(c => c.Id == "left-hand");
        Assert.Equal(RigNodeKinds.Bone, shoulder.Node);
        Assert.Null(shoulder.Link);
        Assert.True(shoulder.Length > .3f);
        Assert.True(elbow.Link is { IsTip: true } && hand.Link is { IsTip: true });
        Assert.Equal(0, hand.Length);
        Assert.All(person.Controls.Where(c => !c.Id.StartsWith("left-", StringComparison.Ordinal) || c.Id.EndsWith("hip") || c.Id.EndsWith("knee") || c.Id.EndsWith("foot")), c => Assert.Equal(RigNodeKinds.Point, c.Node));
        // Every chain stays on the declared point solver: the position-only adapter, listed in LegacyPointConstraint.
        Assert.All(person.IkChains, c => Assert.Equal(ModelChain.PointSolver, c.Solver));

        var points = CharacterModel.FromJson(person.ToJson());
        var report = RigConversion.ToPointControls(points, ["left-shoulder", "left-elbow", "left-hand"]);
        Assert.Equal(3, report.Count);
        var back = CharacterModel.FromJson(points.ToJson());
        RigConversion.ToBoneChain(back, ["left-shoulder", "left-elbow", "left-hand"]);
        Assert.Equal(person.ToJson(), back.ToJson());

        var bones = Catalog.Resolve("person");
        var legacy = ResolvedModel.From(points);
        foreach (var variant in new ModelVariant?[] { null, Catalog.Variants["tall-thin"], Catalog.Variants["brute"] })
        {
            var a = variant is null ? bones : ResolvedModel.From(person, variant);
            var b = variant is null ? legacy : ResolvedModel.From(points, variant);
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
                    Assert.InRange(Vector3.Distance(sa.XAxis, sb.XAxis), 0, 1e-5f);
                }
                Assert.Equal(pa.Chains.Select(c => (c.Chain, c.Reached)), pb.Chains.Select(c => (c.Chain, c.Reached)));
                // The converted frames point along the limb; the hand, with no next bone, keeps the character axes.
                Assert.Equal(pa.Angles["left-hand"], pb.Angles["left-hand"], 5);
                Assert.NotEqual(pa.Angles["left-shoulder"], pb.Angles["left-shoulder"]);
            }
        }
    }

    [Fact]
    public void ThePersonBuildRuleFitsTheBoneChainsDimensionsFromItsPositions()
    {
        var person = Catalog.Models["person"];
        var tall = ResolvedModel.From(person, Catalog.Variants["tall-thin"]);
        var standard = Catalog.Resolve("person");
        var ratio = tall.Nodes["left-shoulder"].Length / standard.Nodes["left-shoulder"].Length;
        Assert.Equal(PersonBuild.TallThin.Arms, ratio, 4);
        Assert.Equal(PersonBuild.TallThin.Arms, tall.Nodes["left-elbow"].Length / standard.Nodes["left-elbow"].Length, 4);
        Assert.Equal(tall.Length("left-shoulder", "left-elbow"), tall.Nodes["left-shoulder"].Length, 5);
        Assert.Equal(standard.Nodes["left-shoulder"].Rotation, tall.Nodes["left-shoulder"].Rotation, 4);
        // The same arms value on the mirrored right arm, still a point chain, scales the same way.
        Assert.Equal(ratio, tall.Length("right-shoulder", "right-elbow") / standard.Length("right-shoulder", "right-elbow"), 4);
        var frozen = new ModelVariant { Id = "frozen", Name = "Frozen", Base = "person", Rest = { ["left-elbow"] = new(0, 1) } };
        Assert.Contains("bone node", Assert.Throws<InvalidDataException>(() => ResolvedModel.From(person, frozen)).Message);
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
        Assert.True(turned.Chains.Single().Reached);
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
