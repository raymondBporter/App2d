using App2d.Core.Characters.Authored;
using App2d.Core.Constraints;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json.Nodes;

namespace App2d.Tests.Authored;

/// <summary>
/// P4b of the character-system migration: angular joint limits on bone nodes, the normalized bend channel against ordinary
/// angle channels, and the limit-aware IK path that returns the closest valid pose with an honest residual.
/// </summary>
public sealed class JointLimitTests
{
    /// <summary>A two-bone arm on the canonical bone solver hanging from a shoulder, optionally beneath a reflected torso.</summary>
    private static CharacterModel Arm(bool reflected = false, bool ik = true, RigLimit? upper = null, RigLimit? elbow = null) => new()
    {
        Id = "arm",
        Name = "Arm",
        Controls =
        [
            new() { Id = "torso", Node = RigNodeKinds.Bone, Local = new() { X = 0, Y = 1, Reflect = reflected }, Length = .5f },
            new() { Id = "upper", Parent = "torso", Node = RigNodeKinds.Bone, Link = new(), Local = new() { Rotation = -MathF.PI / 2 }, Length = .6f, Limit = upper },
            new() { Id = "fore", Parent = "upper", Node = RigNodeKinds.Bone, Link = new(), Local = new() { Rotation = .4f }, Length = .5f, Limit = elbow },
            new() { Id = "hand", Parent = "fore", Node = RigNodeKinds.Bone, Link = new() },
        ],
        // Bend -1 puts the elbow on the side where its local rotation is positive, the side its limits below allow.
        Constraints = ik ? [new ModelChain { Id = "reach", Root = "upper", Joint = "fore", End = "hand", Bend = -1 }] : [],
        Parts = [new() { Id = "fore-art", A = "fore", Frame = "fore", OffsetX = .25f, Geometry = Rectangle2D.Builder.FromSize(.5f, .1f), Material = new() { Fill = "#cccccc", Outline = new() } }],
    };

    private static float Local(EvaluatedPose pose, ResolvedModel model, string id)
    {
        var node = model.Nodes[id];
        var parent = node.Parent is null ? 0 : pose.Angles[node.Parent];
        var hand = node.Parent is null ? 1 : model.Nodes[node.Parent].Handedness;
        return MathF.IEEERemainder(hand * (pose.Angles[id] - parent), MathF.Tau);
    }

    [Fact]
    public void AnAngleChannelIsHeldAtTheLimitWithItsStateRecordedWhileItsKeysKeepTheirValues()
    {
        var model = ResolvedModel.From(Arm(ik: false, elbow: new() { Min = 0, Max = 1.5f }));
        var clip = new MotionClip { Id = "swing", Name = "Swing", Model = "arm", Duration = 1, Tracks = [new() { Kind = MotionClip.RotateKind, Target = "fore", Keys = [new() { Angle = -.5f }, new() { Time = 1, Angle = 2 }] }] };
        clip.Validate(model);
        var low = PoseEvaluator.Sample(model, clip, 0);
        Assert.Equal(0, Local(low, model, "fore"), 5);
        Assert.Equal(ConstraintLimitState1D.Lower, low.Limits["fore"].State);
        Assert.Equal(-.1f, low.Limits["fore"].Error, 5); // the requested .4 - .5 = -.1 lies below the bound by .1
        var mid = PoseEvaluator.Sample(model, clip, .5);
        Assert.Equal(.4f + .75f, Local(mid, model, "fore"), 5);
        Assert.Equal(ConstraintLimitState1D.Inactive, mid.Limits["fore"].State);
        var high = PoseEvaluator.Sample(model, clip, 1);
        Assert.Equal(1.5f, Local(high, model, "fore"), 5);
        Assert.Equal(ConstraintLimitState1D.Upper, high.Limits["fore"].State);
        // The keys are untouched: widening the limit changes what is shown, not what was authored.
        Assert.Equal(2, clip.Tracks[0].Keys[1].Angle);
        Assert.Empty(PoseEvaluator.Rest(ResolvedModel.From(Arm(ik: false))).Limits);
    }

    [Fact]
    public void ANormalizedBendMapsThroughTheLimitsExactlyAtBothEndsAndFollowsLimitChanges()
    {
        var limited = Arm(ik: false, elbow: new() { Min = -.5f, Max = 2.5f });
        var model = ResolvedModel.From(limited);
        var clip = new MotionClip
        {
            Id = "bend",
            Name = "Bend",
            Model = "arm",
            Duration = 1,
            Tracks = [new() { Kind = MotionClip.BendKind, Target = "fore", Keys = [new() { Value = Vector3.Zero }, new() { Time = .5f, Value = new(.5f, 0, 0) }, new() { Time = 1, Value = new(1, 0, 0) }] }],
        };
        clip.Validate(model);
        Assert.Equal(-.5f, Local(PoseEvaluator.Sample(model, clip, 0), model, "fore"), 5);
        Assert.Equal(1, Local(PoseEvaluator.Sample(model, clip, .5), model, "fore"), 5);
        Assert.Equal(2.5f, Local(PoseEvaluator.Sample(model, clip, 1), model, "fore"), 5);
        // The mapping itself is exact at both ends: no float drift at a limit.
        var limit = limited.Controls[2].Limit!;
        Assert.Equal(limit.Min, limit.FromNormalized(0));
        Assert.Equal(limit.Max, limit.FromNormalized(1));
        Assert.Equal(limit.Max, limit.FromNormalized(1.2f));
        Assert.Equal(ChannelSemantics.NormalizedParameter, ChannelSemantics.OfTrack(MotionClip.BendKind));

        // Narrowing the limits changes the normalized performance by design; an angle channel on another clip is unchanged.
        limited.Controls[2].Limit = new() { Min = 0, Max = 1 };
        var narrow = ResolvedModel.From(limited);
        Assert.Equal(.5f, Local(PoseEvaluator.Sample(narrow, clip, .5), narrow, "fore"), 5);
        var angled = new MotionClip { Id = "angle", Name = "Angle", Model = "arm", Duration = 1, Tracks = [new() { Kind = MotionClip.RotateKind, Target = "fore", Keys = [new() { Angle = .3f }] }] };
        angled.Validate(narrow);
        Assert.Equal(.7f, Local(PoseEvaluator.Sample(narrow, angled, 0), narrow, "fore"), 5);

        // Setup before the first key keeps the setup rotation; the bend survives a JSON round trip as its own track kind.
        clip.Tracks[0].SetupBeforeFirst = true;
        clip.Tracks[0].Keys.RemoveAt(0);
        Assert.Equal(.4f, Local(PoseEvaluator.Sample(model, clip, .1), model, "fore"), 5);
        var json = clip.ToJson();
        Assert.Contains("\"kind\": \"bend\"", json);
        Assert.Equal(json, MotionClip.FromJson(json).ToJson());
        Assert.Contains(new Channel(MotionClip.BendKind, "fore"), ClipAuthoring.ChannelsForControl(model, "fore"));
        Assert.DoesNotContain(ClipAuthoring.ChannelsForControl(model, "upper"), c => c.Kind == MotionClip.BendKind);
    }

    [Theory]
    [InlineData("no-limits")]
    [InlineData("both")]
    [InlineData("range")]
    [InlineData("solved")]
    public void BendChannelsAreValidatedAgainstTheirOwner(string failure)
    {
        var model = ResolvedModel.From(Arm(elbow: new() { Min = 0, Max = 2 }));
        var clip = new MotionClip { Id = "bend", Name = "Bend", Model = "arm", Duration = 1 };
        switch (failure)
        {
            case "no-limits":
                clip.Tracks.Add(new() { Kind = MotionClip.BendKind, Target = "torso", Keys = [new() { Value = new(.5f, 0, 0) }] });
                break;
            case "both":
                model = ResolvedModel.From(Arm(ik: false, elbow: new() { Min = 0, Max = 2 }));
                clip.Tracks.Add(new() { Kind = MotionClip.BendKind, Target = "fore", Keys = [new() { Value = new(.5f, 0, 0) }] });
                clip.Tracks.Add(new() { Kind = MotionClip.RotateKind, Target = "fore", Keys = [new() { Angle = .1f }] });
                break;
            case "range":
                model = ResolvedModel.From(Arm(ik: false, elbow: new() { Min = 0, Max = 2 }));
                clip.Tracks.Add(new() { Kind = MotionClip.BendKind, Target = "fore", Keys = [new() { Value = new(1.5f, 0, 0) }] });
                break;
            case "solved":
                clip.Tracks.Add(new() { Kind = MotionClip.BendKind, Target = "fore", Keys = [new() { Value = new(.5f, 0, 0) }] });
                break;
        }
        var message = Assert.Throws<InvalidDataException>(() => clip.Validate(model)).Message;
        Assert.Contains(failure switch { "no-limits" => "declared joint limits", "both" => "not both", "range" => "0..1", _ => "solved by IK" }, message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheLimitAwareSolveStaysInRangeAndReportsUnreachableTargetsHonestlyUnderReflection(bool reflected)
    {
        var model = ResolvedModel.From(Arm(reflected, elbow: new() { Min = 0, Max = 2.4f }));
        var rest = PoseEvaluator.Rest(model);
        var shoulder = rest.World("upper");
        // The hand hangs about 1.07 below the shoulder at rest (upper .6 straight down, fore .5 bent by .4).
        MotionClip Reach(Vector3 target) => new() { Id = "reach", Name = "Reach", Model = "arm", Duration = 1, Tracks = [new() { Kind = MotionClip.TargetKind, Target = "reach", Keys = [new() { Value = target - model.Rest["hand"] }] }] };
        EvaluatedPose Solve(Vector3 target)
        {
            var clip = Reach(target);
            clip.Validate(model);
            return PoseEvaluator.Sample(model, clip, 0);
        }

        // Offsets in the arm's own sense: the reflected torso flips the arm vertically, so the mirrored case mirrors Y.
        Vector3 Off(float x, float y) => new(x, reflected ? -y : y, 0);

        // Reachable within the limits: the analytic pose is exact and the elbow bends inside its range.
        var inside = Solve(shoulder + Off(.3f, -.8f));
        var reach = inside.Chains.Single();
        Assert.True(reach.Reached, $"residual {reach.Residual}");
        Assert.False(reach.Limited);
        Assert.InRange(Local(inside, model, "fore"), 0, 2.4f);
        TestModels.Near(shoulder + Off(.3f, -.8f), inside.World("hand"), 1e-5f);

        // Reachable only by folding the elbow past its upper limit (a target close to the shoulder): the solve holds the
        // elbow at its bound, aims the shape at the target and reports the gap it left instead of claiming the target.
        var close = Solve(shoulder + Off(.1f, -.17f));
        var held = close.Chains.Single();
        Assert.True(held.Limited);
        Assert.False(held.Reached);
        Assert.InRange(held.Residual, .15f, .3f);
        Assert.InRange(Local(close, model, "fore"), -1e-5f, 2.4f + 1e-5f);
        Assert.Equal(ConstraintLimitState1D.Upper, close.Limits["fore"].State);
        Assert.Equal(.6f, Vector3.Distance(close.World("upper"), close.World("fore")), 4);
        Assert.Equal(.5f, Vector3.Distance(close.World("fore"), close.World("hand")), 4);
        // The hand is the closest point of the held shape's sweep, so it lies on the shoulder-target ray.
        var toTarget = Vector3.Normalize(Off(.1f, -.17f));
        var toHand = Vector3.Normalize(close.World("hand") - shoulder);
        Assert.Equal(1, Vector3.Dot(toTarget, toHand), 3);

        // Beyond full reach: straight arm, honest residual, within limits.
        var far = Solve(shoulder + Off(0, -3));
        Assert.False(far.Chains.Single().Reached);
        Assert.Equal(3 - 1.1f, far.Chains.Single().Residual, 3);
        Assert.InRange(Local(far, model, "fore"), -1e-5f, 2.4f + 1e-5f);
    }

    [Fact]
    public void ReflectionAndFacingDoNotInvertTheLimitConvention()
    {
        var plain = ResolvedModel.From(Arm(elbow: new() { Min = 0, Max = 2.4f }));
        var mirrored = ResolvedModel.From(Arm(reflected: true, elbow: new() { Min = 0, Max = 2.4f }));
        var plainRest = PoseEvaluator.Rest(plain);
        var mirrorRest = PoseEvaluator.Rest(mirrored);
        // The same local target offset, hence the same parent-local pose, in both rigs: the local rotations agree and so do the limit states.
        foreach (var offset in new[] { new Vector3(.3f, -.8f, 0), new Vector3(.1f, -.17f, 0), new Vector3(0, -3, 0) })
        {
            // The reflected torso flips the arm vertically, so the same arm-relative target has its Y mirrored in character space.
            EvaluatedPose Solve(ResolvedModel model, EvaluatedPose rest)
            {
                var local = ReferenceEquals(model, mirrored) ? offset with { Y = -offset.Y } : offset;
                var clip = new MotionClip { Id = "reach", Name = "Reach", Model = "arm", Duration = 1, Tracks = [new() { Kind = MotionClip.TargetKind, Target = "reach", Keys = [new() { Value = local }] }] };
                clip.Validate(model);
                return PoseEvaluator.Sample(model, clip, 0);
            }
            var a = Solve(plain, plainRest);
            var b = Solve(mirrored, mirrorRest);
            Assert.Equal(Local(a, plain, "fore"), Local(b, mirrored, "fore"), 4);
            Assert.Equal(a.Limits["fore"].State, b.Limits["fore"].State);
            Assert.Equal(a.Chains[0].Reached, b.Chains[0].Reached);
            Assert.Equal(a.Chains[0].Residual, b.Chains[0].Residual, 4);
            // A mirrored actor (facing -1) is a placement of the same pose: nothing about the limits changes.
            var faced = new ActorPose(a, Vector2.Zero, -1);
            Assert.Equal(-a.World("hand").X, faced.World("hand").X, 5);
        }
    }

    [Fact]
    public void ReachedIsResidualBasedOnTheLinkageAndTheZeroLengthFoldReportsItsGap()
    {
        var linkage = ResolvedModel.From(TestModels.Linkage());
        var crank = TestModels.LinkageCrank(linkage);
        var eighth = PoseEvaluator.Sample(linkage, crank, 1 / 8.0, true).Chains.Single();
        Assert.Equal(.0055427f, eighth.Residual, 5); // the rigid solve's gap (P11c; the point adapter's inset reported .0055562)
        Assert.False(eighth.Reached, "a target 5.6e-3 past full reach is not reached");
        Assert.True(PoseEvaluator.Sample(linkage, crank, 0, true).Chains.Single().Reached);
        var zero = PoseEvaluator.Sample(linkage, TestModels.LinkageReach(linkage), 0).Chains.Single();
        Assert.Equal(0, zero.Residual, 4);
        Assert.True(zero.Reached, "the rigid solve folds the rod fully onto its root (P11c; the point adapter kept a 1e-3 minimum fold)");
        Assert.True(ChainResult.Of("x", ChainResult.ReachTolerance).Reached);
        Assert.False(ChainResult.Of("x", ChainResult.ReachTolerance * 2).Reached);
    }

    [Fact]
    public void LimitsAreDeclaredPropertiesWithOverridesProvenanceAndFlattening()
    {
        var model = Arm(elbow: new() { Min = 0, Max = 2 });
        var fore = PropertyRef.OfControl("fore", RigProperties.LimitMax);
        Assert.Contains(RigProperties.Declarations(model.Controls[2]), d => d.Name == RigProperties.LimitMax && d.Units == ParameterUnits.Radians);
        Assert.DoesNotContain(RigProperties.Declarations(model.Controls[1]), d => RigProperties.LimitProperties.Contains(d.Name));
        Assert.Contains("no joint limits declared", RigProperties.Unsupported(model.Controls[1]).Single().Reason);
        Assert.Empty(RigProperties.Unsupported(model.Controls[2]));

        var variant = new ModelVariant { Id = "stiff", Name = "Stiff", Base = "arm" };
        variant.Set(PropertyOverride.Of(fore, 1.2f));
        var resolved = ResolvedModel.From(model, variant);
        Assert.Equal(new RigLimit { Min = 0, Max = 1.2f }, resolved.Nodes["fore"].Limit);
        Assert.Equal(PropertySource.Override, resolved.Provenance(fore));
        Assert.Equal(2, resolved.AuthoredNumber(fore));
        Assert.Equal(new RigLimit { Min = 0, Max = 2 }, model.Controls[2].Limit); // the base is untouched
        var bendClip = new MotionClip { Id = "bend", Name = "Bend", Model = "arm", Duration = 1, Tracks = [new() { Kind = MotionClip.BendKind, Target = "fore", Keys = [new() { Value = new(1, 0, 0) }] }] };
        var unsolved = ResolvedModel.From(Arm(ik: false, elbow: new() { Min = 0, Max = 2 }), variant);
        bendClip.Validate(unsolved);
        Assert.Equal(1.2f, Local(PoseEvaluator.Sample(unsolved, bendClip, 0), unsolved, "fore"), 5);

        string Refusal(PropertyOverride change)
        {
            var odd = new ModelVariant { Id = "odd", Name = "Odd", Base = "arm", Overrides = [change] };
            return Assert.Throws<InvalidDataException>(() => ResolvedModel.From(model, odd)).Message;
        }
        Assert.Contains("no joint limits declared", Refusal(PropertyOverride.Of(PropertyRef.OfControl("upper", RigProperties.LimitMin), -1)));
        Assert.Contains("outside its joint limits", Refusal(PropertyOverride.Of(PropertyRef.OfControl("fore", RigProperties.LimitMin), 1)));

        var flat = ModelAuthoring.Flatten(resolved, "flat", "Flat");
        Assert.Equal(new RigLimit { Min = 0, Max = 1.2f }, flat.Controls.Single(c => c.Id == "fore").Limit);
        var json = model.ToJson();
        Assert.Contains("\"limit\": {", json);
        Assert.Equal(json, CharacterModel.FromJson(json).ToJson());
        var limitless = JsonNode.Parse(json)!.AsObject();
        Assert.Null(limitless["controls"]![1]!["limit"]);
    }

    [Theory]
    [InlineData("point")]
    [InlineData("rest")]
    [InlineData("inverted")]
    public void LimitsAreRefusedWhereTheyBoundNothing(string failure)
    {
        var model = Arm(elbow: new() { Min = 0, Max = 2 });
        switch (failure)
        {
            case "point":
                model.Controls.Add(new() { Id = "tag", Parent = "torso", Rest = new(0, 2), Limit = new() });
                break;
            case "rest":
                model.Controls[2].Limit = new() { Min = 1, Max = 2 }; // the rest rotation .4 lies below
                break;
            case "inverted":
                model.Controls[2].Limit = new() { Min = 2, Max = 0 };
                break;
        }
        var message = Assert.Throws<InvalidDataException>(model.Validate).Message;
        Assert.Contains(failure switch { "point" => "belong to bone nodes", "rest" => "outside its joint limits", _ => "must not exceed" }, message);
        if (failure == "point")
        {
            // The authoring command refuses by name and leaves the model valid.
            var points = TestModels.LinkagePoints();
            Assert.Contains("bone nodes", Assert.Throws<InvalidOperationException>(() => ModelAuthoring.SetLimit(points, "hub", new())).Message);
            var arm = Arm(elbow: new() { Min = 0, Max = 2 });
            Assert.Contains("outside its joint limits", Assert.Throws<InvalidDataException>(() => ModelAuthoring.SetLimit(arm, "fore", new() { Min = 1, Max = 2 })).Message);
            Assert.Equal(new RigLimit { Min = 0, Max = 2 }, arm.Controls[2].Limit);
            ModelAuthoring.SetLimit(arm, "fore", null);
            Assert.Null(arm.Controls[2].Limit);
        }
    }
}
