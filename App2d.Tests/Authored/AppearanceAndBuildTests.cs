using App2d.Core.Characters;

namespace App2d.Tests.Authored;

public sealed class AppearanceAndBuildTests
{
    private static readonly CharacterModel Person = PersonTemplate.Model();

    [Fact]
    public void BuildValuesResolveThroughTheRuleAndFollowBaseEdits()
    {
        var variant = PersonBuild.TallThin.Apply(Person, "tall", "Tall");
        Assert.Empty(variant.Rest);
        var tall = ResolvedModel.From(Person, variant);
        Assert.True(tall.Rest["head"].Y > ResolvedModel.From(Person).Rest["head"].Y + .2f);
        // A base edit to an unoverridden value reaches the variant through its build.
        var edited = PersonTemplate.Model(); var head = edited.Controls.Single(c => c.Id == "head"); head.Rest = head.Rest with { Y = head.Rest.Y + .1f };
        Assert.Equal(tall.Rest["head"].Y + .1f * PersonBuild.TallThin.Torso, ResolvedModel.From(edited, variant).Rest["head"].Y, 4);
    }

    [Fact]
    public void ExplicitRestOverridesWinOverTheBuild()
    {
        var variant = PersonBuild.TallThin.Apply(Person, "tall", "Tall"); variant.Rest["head"] = new(0, 3);
        Assert.Equal(3, ResolvedModel.From(Person, variant).Rest["head"].Y);
    }

    [Fact]
    public void UnknownOrOutOfRangeBuildValuesFailToResolve()
    {
        var variant = new ModelVariant { Id = "odd", Name = "Odd", Base = "person", Build = { ["tail"] = 1.2f } };
        Assert.Contains("build.tail", Assert.Throws<InvalidDataException>(() => ResolvedModel.From(Person, variant)).Message);
        variant.Build = new() { ["legs"] = 5 };
        Assert.Contains("build.legs", Assert.Throws<InvalidDataException>(() => ResolvedModel.From(Person, variant)).Message);
        Assert.Throws<InvalidDataException>(() => ResolvedModel.From(TestModels.Creature(), new ModelVariant { Id = "x", Name = "X", Base = "creature", Build = { ["legs"] = 1.1f } }));
    }

    [Fact]
    public void ExpressionFallsBackFromGameplayToClipToModelDefault()
    {
        var model = ResolvedModel.From(Person);
        var face = model.Parts.Single(p => p.Face != "none");
        var clip = new MotionClip { Id = "blink", Name = "Blink", Model = "person", Duration = 1, Faces = [new() { Part = face.Id, Keys = [new() { Time = .5f, Expression = "blink" }] }] };
        clip.Validate(model);
        Assert.Equal("blink", PoseEvaluator.Sample(model, clip, .1).Expressions[face.Id]);
        Assert.Equal("blink", PoseEvaluator.Sample(model, clip, .7).Expressions[face.Id]);
        Assert.Equal("hurt", PoseEvaluator.Sample(model, clip, .7, input: new("hurt")).Expressions[face.Id]);
        Assert.Equal(face.Face, PoseEvaluator.Rest(model).Expressions[face.Id]);
    }

    [Fact]
    public void AGameplayExpressionNeverMovesTheBody()
    {
        var model = ResolvedModel.From(Person); var clip = AuthoredCatalog.Load(TestModels.AuthoredRoot).Animations["person-walk"];
        var plain = PoseEvaluator.Sample(model, clip, .4); var hurt = PoseEvaluator.Sample(model, clip, .4, input: new("hurt"));
        foreach (var (id, point) in plain.Points) Assert.Equal(point, hurt.Points[id]);
    }

    [Fact]
    public void AHiddenPartKeepsItsControlsAndDrawsNoFace()
    {
        var head = Person.Parts.Single(p => p.Face != "none").Id;
        var headless = new ModelVariant { Id = "headless", Name = "Headless", Base = "person", Parts = { [head] = new() { Hidden = true } } };
        var model = ResolvedModel.From(Person, headless);
        var pose = PoseEvaluator.Rest(model);
        Assert.Contains("head", pose.Points.Keys);
        Assert.DoesNotContain(head, pose.Expressions.Keys);
    }

    [Theory, InlineData(ClipEase.Linear, .5f), InlineData(ClipEase.Smooth, .5f), InlineData(ClipEase.Step, 0)]
    public void KeysEaseTowardTheNextKey(string ease, float middle)
    {
        var keys = new List<ClipKey> { new() { Time = 0, Y = 0, Ease = ease }, new() { Time = 1, Y = 1 } };
        Assert.Equal(middle, PoseEvaluator.Interpolate(keys, .5f).Value.Y, 4);
        Assert.Equal(1, PoseEvaluator.Interpolate(keys, 1).Value.Y, 4);
        if (ease == ClipEase.Smooth) Assert.True(PoseEvaluator.Interpolate(keys, .1f).Value.Y < .1f);
    }

    [Fact]
    public void MarkersAndFaceKeysAreValidated()
    {
        var clip = new MotionClip { Id = "c", Name = "C", Model = "person", Markers = [new() { Id = "step", Time = .2f }, new() { Id = "step", Time = .4f }] };
        Assert.Contains("duplicate marker", Assert.Throws<InvalidDataException>(clip.Validate).Message);
        clip.Markers = [new() { Id = "late", Time = 3 }];
        Assert.Throws<InvalidDataException>(clip.Validate);
        clip.Markers = []; clip.Faces = [new() { Part = "head", Keys = [new() { Expression = "grinning" }] }];
        Assert.Contains("grinning", Assert.Throws<InvalidDataException>(clip.Validate).Message);
    }
}
