using App2d.CharacterSeeds;
using App2d.Core.Characters.Authored;

namespace App2d.Tests.Authored;

public sealed class AppearanceAndBuildTests
{
    private static readonly CharacterModel Person = PersonTemplate.Model();

    [Fact]
    public void BuildValuesResolveThroughTheRuleAndFollowBaseEdits()
    {
        var variant = PersonBuild.TallThin.Apply(Person, "tall", "Tall");
        Assert.Empty(variant.Overrides);
        var tall = ResolvedModel.From(Person, variant);
        Assert.True(tall.Rest["head"].Y > ResolvedModel.From(Person).Rest["head"].Y + .2f);
        // A base edit to an unoverridden value reaches the variant through its build.
        var edited = PersonTemplate.Model();
        var head = edited.Controls.Single(c => c.Id == "head");
        head.Rest = head.Rest with
        {
            Y = head.Rest.Y + .1f
        };
        Assert.Equal(tall.Rest["head"].Y + .1f * PersonBuild.TallThin.Torso, ResolvedModel.From(edited, variant).Rest["head"].Y, 4);
    }

    [Fact]
    public void ExplicitRestOverridesWinOverTheBuild()
    {
        var variant = PersonBuild.TallThin.Apply(Person, "tall", "Tall");
        var tall = ResolvedModel.From(Person, variant);
        ModelAuthoring.MoveRest(tall, variant, "head", new(0, 3, tall.Rest["head"].Z), children: true);
        Assert.Equal(3, ResolvedModel.From(Person, variant).Rest["head"].Y, 5);
        Assert.Equal(PropertySource.Override, ResolvedModel.From(Person, variant).Provenance(PropertyRef.OfControl("head", RigProperties.LocalY)));
    }

    [Fact]
    public void UnknownOrOutOfRangeBuildValuesFailToResolve()
    {
        var variant = new ModelVariant { Id = "odd", Name = "Odd", Base = "person", Parameters = { ["tail"] = 1.2f } };
        Assert.Contains("build.tail", Assert.Throws<InvalidDataException>(() => ResolvedModel.From(Person, variant)).Message);
        variant.Parameters = new()
        {
            ["legs"] = 5
        };
        Assert.Contains("build.legs", Assert.Throws<InvalidDataException>(() => ResolvedModel.From(Person, variant)).Message);
        Assert.Throws<InvalidDataException>(() => ResolvedModel.From(TestModels.Creature(), new ModelVariant { Id = "x", Name = "X", Base = "creature", Parameters = { ["legs"] = 1.1f } }));
    }

    [Fact]
    public void ExpressionFallsBackFromGameplayToClipToInstanceDefault()
    {
        // The face is the baked part instanced on the head socket (P7c); the clip's selection channel and a gameplay request address it by instance.
        var model = ResolvedModel.From(PersonTemplate.Model(face: true), null, PersonTemplate.PartOf);
        var face = PersonTemplate.FaceInstance;
        var clip = new MotionClip { Id = "blink", Name = "Blink", Model = "person", Duration = 1, Plays = [new() { Instance = face, Keys = [new() { Time = .5f, Expression = "blink" }] }] };
        clip.Validate(model);
        Assert.Equal("blink", PoseEvaluator.Sample(model, clip, .1).Expressions[face]);
        Assert.Equal("blink", PoseEvaluator.Sample(model, clip, .7).Expressions[face]);
        Assert.Equal("hurt", PoseEvaluator.Sample(model, clip, .7, input: new("hurt")).Expressions[face]);
        Assert.Equal("relaxed", PoseEvaluator.Rest(model).Expressions[face]);
    }

    [Fact]
    public void AClipCanHideTheFaceForABackView()
    {
        var model = ResolvedModel.From(PersonTemplate.Model(face: true), null, PersonTemplate.PartOf);
        var clip = new MotionClip { Id = "climb", Name = "Climb", Model = "person", Duration = 1, Plays = [new() { Instance = PersonTemplate.FaceInstance, Keys = [new() { Expression = PartAsset.None }] }] };
        clip.Validate(model);
        var pose = PoseEvaluator.Sample(model, clip, .5);
        Assert.Equal(PartAsset.None, pose.Expressions[PersonTemplate.FaceInstance]);
        Assert.True(pose.Attachments[PersonTemplate.FaceInstance].Hidden);
    }

    [Fact]
    public void ATargetKeyFlipsItsChainsBendFromThatKeyOn()
    {
        var model = ResolvedModel.From(Person);
        MotionClip Reach(int? bend) => new()
        {
            Id = "reach",
            Name = "Reach",
            Model = "person",
            Duration = 1,
            Reference = model.Measures.ToDictionary(p => p.Key, p => p.Value),
            Tracks = [new() { Kind = MotionClip.TargetKind, Target = "left-arm", Keys = [new() { Value = new(-.2f, .9f, 0) }, new() { Time = .5f, Value = new(-.2f, .9f, 0), Bend = bend }] }],
        };
        float ElbowSide(MotionClip clip, double seconds)
        {
            clip.Validate(model);
            var pose = PoseEvaluator.Sample(model, clip, seconds);
            var shoulder = pose.Points["left-shoulder"];
            var hand = pose.Points["left-hand"];
            var elbow = pose.Points["left-elbow"];
            return MathF.Sign((hand.X - shoulder.X) * (elbow.Y - shoulder.Y) - (hand.Y - shoulder.Y) * (elbow.X - shoulder.X));
        }
        var flipped = Reach(1);
        Assert.Equal(ElbowSide(Reach(null), .75), ElbowSide(flipped, .25));
        Assert.Equal(-ElbowSide(flipped, .25), ElbowSide(flipped, .75));
        Assert.DoesNotContain("\"bend\"", Reach(null).ToJson());
        var invalid = Reach(2);
        Assert.Throws<InvalidDataException>(() => invalid.Validate(model));
    }

    [Fact]
    public void AGameplayExpressionNeverMovesTheBody()
    {
        var model = ResolvedModel.From(Person);
        var clip = AuthoredCatalog.Load(TestModels.AuthoredRoot).Animations["person-walk"];
        var plain = PoseEvaluator.Sample(model, clip, .4);
        var hurt = PoseEvaluator.Sample(model, clip, .4, input: new("hurt"));
        foreach (var (id, point) in plain.Points)
        {
            Assert.Equal(point, hurt.Points[id]);
        }
    }

    [Fact]
    public void AHiddenPartKeepsItsControlsAndDrawsNoFace()
    {
        var head = "head";
        var headless = new ModelVariant { Id = "headless", Name = "Headless", Base = "person", Overrides = [PropertyOverride.Of(PropertyRef.OfPart(head, RigProperties.Hidden), true)] };
        var model = ResolvedModel.From(Person, headless);
        var pose = PoseEvaluator.Rest(model);
        Assert.Contains("head", pose.Points.Keys);
        Assert.DoesNotContain(head, pose.Expressions.Keys);
    }

    [Theory, InlineData(ClipEase.Linear, .5f), InlineData(ClipEase.Smooth, .5f), InlineData(ClipEase.Step, 0)]
    public void KeysEaseTowardTheNextKey(string ease, float middle)
    {
        var keys = new List<ClipKey> { new() { Time = 0, Value = new(0, 0, 0), Ease = ease }, new() { Time = 1, Value = new(0, 1, 0) } };
        Assert.Equal(middle, PoseEvaluator.Interpolate(keys, .5f).Value.Y, 4);
        Assert.Equal(1, PoseEvaluator.Interpolate(keys, 1).Value.Y, 4);
        if (ease == ClipEase.Smooth)
        {
            Assert.True(PoseEvaluator.Interpolate(keys, .1f).Value.Y < .1f);
        }
    }

    [Fact]
    public void MarkersAndFaceKeysAreValidated()
    {
        var clip = new MotionClip { Id = "c", Name = "C", Model = "person", Markers = [new() { Id = "step", Time = .2f }, new() { Id = "step", Time = .4f }] };
        Assert.Contains("duplicate marker", Assert.Throws<InvalidDataException>(clip.Validate).Message);
        clip.Markers = [new() { Id = "late", Time = 3 }];
        Assert.Throws<InvalidDataException>(clip.Validate);
        clip.Markers = [];
        clip.Plays = [new() { Instance = "face", Keys = [new() { Expression = "Grinning!" }] }];
        Assert.Contains("expression", Assert.Throws<InvalidDataException>(clip.Validate).Message);
    }
}
