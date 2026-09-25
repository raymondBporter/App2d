using System.Numerics;
using App2d.Core.Characters;

namespace App2d.Tests.Authored;

/// <summary>The phase-one gate: one shared walk and run, no copied keys, correct on three builds.</summary>
public sealed class PersonVariantMotionTests
{
    private static readonly CharacterModel Person = PersonTemplate.Model();

    private static MotionClip Clip(string which)
    {
        var puppet = which == "walk" ? PuppetTemplates.StepStudy() : PuppetTemplates.RunStudy();
        return PuppetMotionConverter.Convert(puppet, puppet.Motions[0], Person, "person-" + which, which, "leg");
    }
    private static PersonBuild BuildValues(string build) => build switch { "tall" => PersonBuild.TallThin, "short" => PersonBuild.ShortBroad, _ => new() };
    private static ResolvedModel Build(string build) =>
        build == "standard" ? ResolvedModel.From(Person) : ResolvedModel.From(Person, BuildValues(build).Apply(Person, build + "-build", build));
    private static Vector2 XY(Vector3 v) => new(v.X, v.Y);
    private static IEnumerable<double> TwoCycles(MotionClip clip) => Enumerable.Range(0, 481).Select(i => i * clip.Duration / 240.0);

    public static TheoryData<string, string> Cases => new()
    {
        { "walk", "standard" }, { "walk", "tall" }, { "walk", "short" }, { "run", "standard" }, { "run", "tall" }, { "run", "short" },
    };

    [Theory, MemberData(nameof(Cases))]
    public void LimbsKeepTheirLengthsAndEveryTargetIsReached(string clipName, string build)
    {
        var model = Build(build); var clip = Clip(clipName); clip.Validate(model);
        foreach (var seconds in TwoCycles(clip))
        {
            var pose = PoseEvaluator.Sample(model, clip, seconds, repeat: true);
            foreach (var chain in model.Chains.Values)
            {
                Assert.InRange(MathF.Abs(model.Length(chain.Root, chain.Joint) - Vector2.Distance(XY(pose.World(chain.Root)), XY(pose.World(chain.Joint)))), 0, 1e-4f);
                Assert.InRange(MathF.Abs(model.Length(chain.Joint, chain.End) - Vector2.Distance(XY(pose.World(chain.Joint)), XY(pose.World(chain.End)))), 0, 1e-4f);
            }
            Assert.All(pose.Chains, c => Assert.True(c.Reached, $"{c.Chain} misses by {c.Residual} at {seconds:F3}s"));
            Assert.All(pose.Contacts, c => Assert.InRange(c.Residual, 0, 1e-4f));
        }
    }

    [Theory, InlineData("standard"), InlineData("tall"), InlineData("short")]
    public void LimbSegmentsScaleWithTheirBuildValue(string build)
    {
        var model = Build(build); var standard = Build("standard");
        Assert.Equal(standard.Length("left-hip", "left-knee") * BuildValues(build).Legs, model.Length("left-hip", "left-knee"), 4);
        Assert.Equal(standard.Length("right-shoulder", "right-elbow") * BuildValues(build).Arms, model.Length("right-shoulder", "right-elbow"), 4);
    }

    [Theory, MemberData(nameof(Cases))]
    public void PlantedFeetStayPutAndStrideScalesWithLegLength(string clipName, string build)
    {
        var model = Build(build); var clip = Clip(clipName); var standard = Build("standard");
        float Stride(ResolvedModel m) => PoseEvaluator.Sample(m, clip, clip.Duration, repeat: true).Locomotion.X - PoseEvaluator.Sample(m, clip, 0).Locomotion.X;
        Assert.InRange(MathF.Abs(Stride(model) - Stride(standard) * BuildValues(build).Legs), 0, 1e-4f);
        foreach (var contact in clip.Contacts)
        {
            var end = model.Chains[contact.Chain].End;
            var planted = PoseEvaluator.Sample(model, clip, clip.Duration + contact.Start, repeat: true).World(end);
            for (var i = 0; i < 20; i++)
            {
                var t = contact.Start + (contact.Finish - contact.Start) * i / 20f;
                TestModels.Near(planted, PoseEvaluator.Sample(model, clip, clip.Duration + t, repeat: true).World(end), 1e-4f, $"{end} at {t:F3}s");
            }
        }
    }

    [Theory, MemberData(nameof(Cases))]
    public void TheLoopSeamIsContinuous(string clipName, string build)
    {
        var model = Build(build); var clip = Clip(clipName);
        var before = PoseEvaluator.Sample(model, clip, clip.Duration - 1e-4, repeat: true);
        var after = PoseEvaluator.Sample(model, clip, clip.Duration, repeat: true);
        foreach (var id in model.Rest.Keys) TestModels.Near(before.World(id), after.World(id), .01f, id);
        TestModels.Near(before.Locomotion, after.Locomotion, .001f, "travel");
    }

    [Theory, InlineData("walk"), InlineData("run")]
    public void BuildsKeepTheReferenceJointAngles(string clipName)
    {
        var clip = Clip(clipName); var standard = Build("standard");
        static float Bend(EvaluatedPose pose, ModelChain chain)
        {
            var a = Vector2.Normalize(XY(pose.World(chain.Root) - pose.World(chain.Joint)));
            var b = Vector2.Normalize(XY(pose.World(chain.End) - pose.World(chain.Joint)));
            return MathF.Acos(Math.Clamp(Vector2.Dot(a, b), -1, 1)) * 180 / MathF.PI;
        }
        foreach (var build in new[] { "tall", "short" })
        {
            var model = Build(build);
            for (var i = 0; i < 48; i++)
            {
                var seconds = i * clip.Duration / 48.0;
                var a = PoseEvaluator.Sample(standard, clip, seconds); var b = PoseEvaluator.Sample(model, clip, seconds);
                foreach (var chain in standard.Chains.Values) Assert.InRange(MathF.Abs(Bend(a, chain) - Bend(b, chain)), 0, 1f);
            }
        }
    }

    [Fact]
    public void EditingTheSharedClipChangesEveryBuild()
    {
        var clip = Clip("walk"); var builds = new[] { "standard", "tall", "short" }.Select(Build).ToArray();
        var before = builds.Select(m => PoseEvaluator.Sample(m, clip, 0).World("hips").Y).ToArray();
        clip.Tracks.Single(t => t is { Kind: MotionClip.TranslateKind, Target: "hips" }).Keys[0].Y += .1f;
        var after = builds.Select(m => PoseEvaluator.Sample(m, clip, 0).World("hips").Y).ToArray();
        Assert.Equal(.1f, after[0] - before[0], 4);
        Assert.Equal(.1f * PersonBuild.TallThin.Legs, after[1] - before[1], 4);
        Assert.Equal(.1f * PersonBuild.ShortBroad.Legs, after[2] - before[2], 4);
    }

    [Fact]
    public void AnUnreachableBuildKeepsLimbLengthsAndReportsTheShortfall()
    {
        // Straighten the left knee: that leg can no longer reach mid-stance, while the leg measure (right leg) is unchanged.
        var hip = Person.Controls.Single(c => c.Id == "left-hip").Rest.XYZ; var foot = Person.Controls.Single(c => c.Id == "left-foot").Rest.XYZ;
        var variant = new ModelVariant { Id = "stiff-left", Name = "Stiff left", Base = "person", Rest = { ["left-knee"] = PuppetPoint.From(Vector3.Lerp(hip, foot, .5f)) } };
        var model = ResolvedModel.From(Person, variant); var clip = Clip("walk"); clip.Validate(model);
        var misses = 0;
        foreach (var seconds in TwoCycles(clip))
        {
            var pose = PoseEvaluator.Sample(model, clip, seconds, repeat: true);
            Assert.InRange(MathF.Abs(model.Length("left-hip", "left-knee") - Vector2.Distance(XY(pose.World("left-hip")), XY(pose.World("left-knee")))), 0, 1e-4f);
            if (pose.Chains.Single(c => c.Chain == "left-leg") is { Reached: false, Residual: > 1e-3f }) misses++;
        }
        Assert.True(misses > 0, "Expected the straightened leg to report reach errors.");
    }

    [Fact]
    public void BuildValuesAreRangeChecked() =>
        Assert.Contains("legs", Assert.Throws<InvalidDataException>(() => new PersonBuild { Legs = 3 }.Apply(Person, "giant", "Giant")).Message);
}
