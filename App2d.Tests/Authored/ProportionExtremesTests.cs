using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>
/// P4c of the character-system migration: the shared walk, run and attack keys play on proportion extremes, measure-scaled
/// targets, contacts and travel keep their meaning, and the parameter and channel metadata the inspectors show is declared.
/// </summary>
public sealed class ProportionExtremesTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
    private static CharacterModel Person => Catalog.Models["person"];
    private static ResolvedModel Build(PersonBuild build, string id) => ResolvedModel.From(Person, build.Apply(Person, id, id), Catalog.Parts.GetValueOrDefault);
    private static Vector2 XY(Vector3 v) => new(v.X, v.Y);

    public static TheoryData<string, string> Cases => new()
    {
        { "person-walk", "stilted" }, { "person-walk", "squat" }, { "person-run", "stilted" }, { "person-run", "squat" },
        { "player-sword-side-cut", "stilted" }, { "player-sword-side-cut", "squat" }, { "person-hammer-slam", "stilted" }, { "person-hammer-slam", "squat" },
    };

    private static PersonBuild Values(string build) => build == "stilted" ? PersonBuild.Stilted : PersonBuild.Squat;

    [Theory, MemberData(nameof(Cases))]
    public void TheSharedKeysPlayOnExtremeBuildsWithLimbLengthsKeptAndTargetsReachedOrReported(string clipId, string build)
    {
        var model = Build(Values(build), build);
        var standard = Catalog.Resolve("person");
        var clip = Catalog.Animations[clipId];
        clip.Validate(model);
        Assert.Equal(standard.Length("left-hip", "left-knee") * Values(build).Legs, model.Length("left-hip", "left-knee"), 4);
        Assert.Equal(standard.Nodes["left-shoulder"].Length * Values(build).Arms, model.Nodes["left-shoulder"].Length, 4);
        var misses = 0;
        foreach (var seconds in Enumerable.Range(0, 97).Select(i => i * clip.Duration / 48.0))
        {
            var pose = PoseEvaluator.Sample(model, clip, seconds, repeat: true);
            foreach (var chain in model.Chains.Values)
            {
                Assert.InRange(MathF.Abs(model.Length(chain.Root, chain.Joint) - Vector2.Distance(XY(pose.World(chain.Root)), XY(pose.World(chain.Joint)))), 0, 1e-4f);
                Assert.InRange(MathF.Abs(model.Length(chain.Joint, chain.End) - Vector2.Distance(XY(pose.World(chain.Joint)), XY(pose.World(chain.End)))), 0, 1e-4f);
            }

            // Reached is honest: a chain either lands within tolerance or reports its gap; nothing is claimed by clamping.
            foreach (var chain in pose.Chains)
            {
                Assert.Equal(chain.Residual <= ChainResult.ReachTolerance, chain.Reached);
                misses += chain.Reached ? 0 : 1;
            }

            Assert.All(pose.Contacts, c => Assert.InRange(c.Residual, 0, 1e-4f));
        }
        // The walk and run were authored for the leg measure, so every build plants and reaches; the attacks may run short on an extreme arm and say so.
        if (clipId is "person-walk" or "person-run")
        {
            Assert.Equal(0, misses);
        }
    }

    [Theory, InlineData("stilted"), InlineData("squat")]
    public void MeasureScaledContactsAndTravelFollowTheBuildsLegMeasure(string build)
    {
        var model = Build(Values(build), build);
        var standard = Catalog.Resolve("person");
        var walk = Catalog.Animations["person-walk"];
        // The clip carries the leg measure it was authored against (the study reference); each build scales by its own measure over that.
        var ratio = model.Measure(walk.Travel.Scale) / walk.Reference[walk.Travel.Scale];
        var standardRatio = standard.Measure(walk.Travel.Scale) / walk.Reference[walk.Travel.Scale];
        Assert.Equal(model.Measures["leg"] / standard.Measures["leg"], ratio / standardRatio, 4);
        // The leg measure follows the legs parameter (the hip stack scales about the ground line, so not by exactly the factor).
        Assert.True(build == "stilted" ? ratio > 1.3f * standardRatio : ratio < .7f * standardRatio, $"leg ratio {ratio}");
        // Travel and planted targets scale by the same reference-measure ratio the clip was authored against.
        Assert.Equal(PoseEvaluator.CycleTravel(standard, walk).X * ratio / standardRatio, PoseEvaluator.CycleTravel(model, walk).X, 4);
        var contact = walk.Contacts[0];
        var at = (contact.Start + contact.Finish) / 2;
        var standardPose = PoseEvaluator.Sample(standard, walk, at);
        var pose = PoseEvaluator.Sample(model, walk, at);
        var planted = pose.Contacts.Single(c => c.Chain == contact.Chain);
        var standardPlanted = standardPose.Contacts.Single(c => c.Chain == contact.Chain);
        var restEnd = model.Rest[model.Chains[contact.Chain].End];
        var standardRestEnd = standard.Rest[standard.Chains[contact.Chain].End];
        TestModels.Near(standardRestEnd + new Vector3(contact.Target.X * standardRatio, contact.Target.Y * standardRatio, 0) + new Vector3(PoseEvaluator.Sample(standard, walk, 0).Locomotion.X, 0, 0), standardPlanted.Target with { Z = standardRestEnd.Z }, 1e-4f);
        TestModels.Near(restEnd + new Vector3(contact.Target.X * ratio, contact.Target.Y * ratio, 0) + new Vector3(PoseEvaluator.Sample(model, walk, 0).Locomotion.X, 0, 0), planted.Target with { Z = restEnd.Z }, 1e-4f);
        Assert.InRange(planted.Residual, 0, 1e-4f);
    }

    [Fact]
    public void ParameterAndChannelMetadataIsDeclaredForTheInspectors()
    {
        var model = Catalog.Resolve("person");
        foreach (var parameter in model.Parameters.Parameters)
        {
            Assert.Equal(ParameterUnits.Factor, parameter.Units);
            Assert.True(parameter.Range.SoftMin >= parameter.Min && parameter.Range.SoftMax <= parameter.Max, "the editor slider range lies inside the hard range");
            Assert.True(parameter.Range.SoftMin < parameter.Range.SoftMax);
        }

        // The extreme builds sit at the hard range, outside the suggested slider span: the slider span and the hard limit are different responsibilities.
        var legs = model.Parameters.ById["legs"];
        Assert.Equal(legs.Max, PersonBuild.Stilted.Legs);
        Assert.Equal(legs.Min, PersonBuild.Squat.Legs);
        Assert.True(PersonBuild.Stilted.Legs > legs.Range.SoftMax);
        Assert.Contains("legs", Assert.Throws<InvalidDataException>(() => new PersonBuild { Legs = 2.5f }.Apply(Person, "giant", "Giant")).Message);

        // Every track kind maps to a declared channel semantics with units the inspector can show, including the normalized bend.
        foreach (var kind in MotionClip.TrackKinds)
        {
            var declaration = ChannelSemantics.Of(ChannelSemantics.OfTrack(kind, RigNodeKinds.Bone));
            Assert.Contains(declaration.Units, ParameterUnits.All);
        }
        Assert.Equal(ParameterUnits.Normalized, ChannelSemantics.Of(ChannelSemantics.NormalizedParameter).Units);
        Assert.Equal(ParameterUnits.Radians, ChannelSemantics.Of(ChannelSemantics.Angle).Units);
        // A target channel's measure is its chain's declared scale and the travel's its own; the walk carries the reference it was authored against.
        var walk = Catalog.Animations["person-walk"];
        Assert.Equal("leg", model.Chains["left-leg"].Scale);
        Assert.Equal("leg", walk.Travel.Scale);
        Assert.True(walk.Reference.ContainsKey("leg"));
    }
}
