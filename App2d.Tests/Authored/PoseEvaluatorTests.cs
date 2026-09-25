using System.Numerics;
using App2d.Core.Characters;

namespace App2d.Tests.Authored;

public sealed class PoseEvaluatorTests
{
    private static readonly ResolvedModel Model = ResolvedModel.From(TestModels.Creature());

    private static EvaluatedPose Sample(MotionClip clip, double seconds, bool repeat = false, ResolvedModel? model = null)
    {
        clip.Validate(model ?? Model);
        return PoseEvaluator.Sample(model ?? Model, clip, seconds, repeat);
    }

    [Fact]
    public void AnEmptyClipReproducesRest()
    {
        var pose = Sample(TestModels.Clip(Model), .3);
        foreach (var (id, rest) in Model.Rest) TestModels.Near(rest, pose.World(id), 1e-4f, id);
        Assert.All(pose.Chains, c => { Assert.True(c.Reached); Assert.InRange(c.Residual, 0, 1e-4f); });
    }

    [Fact]
    public void TranslationMovesChildrenAndFrameTargetsButNotLocomotionTargets()
    {
        var clip = TestModels.Clip(Model);
        clip.Tracks.Add(new() { Target = "body", Keys = [new() { Y = -.2f }] });
        var pose = Sample(clip, 0);
        TestModels.Near(new(0, .8f, 0), pose.World("body"));
        TestModels.Near(new(0, 1.3f, 0), pose.World("shoulder"));
        TestModels.Near(new(.8f, 1.3f, 0), pose.World("hand"), 1e-4f, "arm target follows the shoulder frame");
        TestModels.Near(Vector3.Zero, pose.World("foot"), 1e-4f, "leg target stays in the locomotion frame");
    }

    [Fact]
    public void RotationIsInheritedByChildOffsetsAndFrames()
    {
        var clip = TestModels.Clip(Model);
        clip.Tracks.Add(new() { Kind = MotionClip.RotateKind, Target = "body", Keys = [new() { Angle = MathF.PI / 2 }] });
        var pose = Sample(clip, 0);
        TestModels.Near(new(-.5f, 1, 0), pose.World("shoulder"));
        TestModels.Near(new(-.5f, 1.8f, 0), pose.World("hand"));
        TestModels.Near(new(0, 1, 0), pose.World("hip"));
    }

    [Fact]
    public void DeltasScaleByVariantOverReferenceMeasureButDepthDoesNot()
    {
        var variant = new ModelVariant { Id = "long-arm", Name = "Long arm", Base = "creature", Rest = { ["elbow"] = new(.6f, 1.2f), ["hand"] = new(1.2f, 1.5f) } };
        var longArm = ResolvedModel.From(TestModels.Creature(), variant);
        var clip = TestModels.Clip(Model);   // reference measures are the base's
        clip.Tracks.Add(new() { Kind = MotionClip.TargetKind, Target = "arm", Keys = [new() { Y = .1f, Z = .3f }] });
        var pose = Sample(clip, 0, model: longArm);
        TestModels.Near(new(1.2f, 1.65f, .3f), pose.World("hand"));
        Assert.Equal(longArm.Length("shoulder", "elbow"), Vector2.Distance(XY(pose.World("shoulder")), XY(pose.World("elbow"))), 4);
    }

    [Fact]
    public void TravelLoopsAccumulateAndOneShotsClamp()
    {
        var clip = TestModels.Clip(Model);
        clip.Travel = new() { Scale = "leg", Keys = [new() { Time = 0 }, new() { Time = 1, X = .4f }] };
        Assert.Equal(.9f, Sample(clip, 2.25, repeat: true).Locomotion.X, 4);
        Assert.Equal(.4f, Sample(clip, 2.25).Locomotion.X, 4);
        clip.Loop = false;
        Assert.Equal(.4f, Sample(clip, 2.25, repeat: true).Locomotion.X, 4);
    }

    [Fact]
    public void ContactsHoldAWorldTargetOverAHalfOpenInterval()
    {
        var clip = TestModels.Clip(Model);
        clip.Travel = new() { Scale = "leg", Keys = [new() { Time = 0 }, new() { Time = 1, X = .4f }] };
        clip.Contacts.Add(new() { Chain = "leg", Start = 0, Finish = .5f });
        var planted = Sample(clip, .25);
        TestModels.Near(Vector3.Zero, planted.World("foot"), 1e-4f, "planted while travelling");
        Assert.Single(planted.Contacts);
        var released = Sample(clip, .5);
        Assert.Empty(released.Contacts);
        TestModels.Near(new(.2f, 0, 0), released.World("foot"), 1e-4f, "released at the finish");
        TestModels.Near(new(.8f, 0, 0), Sample(clip, 2.25, repeat: true).World("foot"), 1e-4f, "planted at the third cycle's start");
    }

    [Fact]
    public void AHeldEndpointIncludesAContactThatFinishesThere()
    {
        var clip = TestModels.Clip(Model); clip.Loop = false; clip.Travel.Scale = "leg";
        clip.Contacts.Add(new() { Chain = "leg", Start = .5f, Finish = 1, Target = new(.1f, 0) });
        Assert.Single(Sample(clip, 1).Contacts);
        Assert.Single(Sample(clip, 5).Contacts);
        clip.Loop = true;
        Assert.Empty(Sample(clip, 1, repeat: true).Contacts);
    }

    [Fact]
    public void AnUnreachableTargetKeepsLengthsAndReportsTheShortfall()
    {
        var clip = TestModels.Clip(Model);
        clip.Tracks.Add(new() { Kind = MotionClip.TargetKind, Target = "leg", Keys = [new() { Y = -5 }] });
        var pose = Sample(clip, 0);
        var leg = pose.Chains.Single(c => c.Chain == "leg");
        Assert.False(leg.Reached); Assert.InRange(leg.Residual, 4.9f, 5.1f); // target is 6 below the hip; the leg reaches ~1.02
        Assert.Equal(Model.Length("hip", "knee"), Vector2.Distance(XY(pose.World("hip")), XY(pose.World("knee"))), 4);
        Assert.Equal(Model.Length("knee", "foot"), Vector2.Distance(XY(pose.World("knee")), XY(pose.World("foot"))), 4);
    }

    [Theory, InlineData(double.NaN), InlineData(-1d), InlineData(double.PositiveInfinity)]
    public void InvalidTimesAreRejected(double seconds) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => PoseEvaluator.Sample(Model, TestModels.Clip(Model), seconds));

    private static Vector2 XY(Vector3 v) => new(v.X, v.Y);
}
