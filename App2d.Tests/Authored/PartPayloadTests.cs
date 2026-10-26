using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Shapes;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>
/// P2c of the character-system migration: common part authoring carries no wave or face assumption. Each optional payload
/// answers for its own geometry support and control references, and the legacy source fields sit behind one named boundary.
/// </summary>
public sealed class PartPayloadTests
{
    private static CharacterModel Person => PersonTemplate.Model();

    [Fact]
    public void DeletingAReferencedControlIsRefusedByTheOwningFeatureAndAttachedPartsGoWithTheControl()
    {
        var model = Person;
        var head = model.Parts.Single(p => p.Id == Humanoid.Head);
        Assert.Empty(head.Payloads.Where(p => p.References(Humanoid.Head)));
        Assert.Null(head.Dependency(Humanoid.Head));
        Assert.True(head.AttachedTo(Humanoid.Head));

        // A wave's phase control: the modifier answers, the command names the owner and the feature.
        model.Controls.Add(new() { Id = "ripple", Parent = Humanoid.Chest });
        var arm = model.Parts.Single(p => p.Id == "left-arm");
        arm.Bindings = null;
        arm.Geometry = new QuadraticBezier2D { Start = Vector2.Zero, Control = new(0, .5f), End = Vector2.UnitY }.Validated();
        arm.Wave = new() { Amplitude = .1f, PhaseControl = "ripple" };
        model.Validate();
        Assert.Equal("wave reference", arm.Dependency("ripple"));
        var refusal = Assert.Throws<InvalidOperationException>(() => ModelAuthoring.RemoveControl(model, "ripple")).Message;
        Assert.Contains("part 'left-arm' uses 'ripple' as a wave reference", refusal);
        Assert.Contains(model.Controls, c => c.Id == "ripple");

        // A point binding: the binding answers.
        model.Controls.Add(new() { Id = "pin", Parent = Humanoid.Hips });
        var body = model.Parts.Single(p => p.Id == Humanoid.Body);
        body.Bindings = [new() { Point = 0, Control = "pin" }];
        model.Validate();
        Assert.Equal("point binding", body.Dependency("pin"));
        Assert.Contains("part 'body' uses 'pin' as a point binding", Assert.Throws<InvalidOperationException>(() => ModelAuthoring.RemoveControl(model, "pin")).Message);
        body.Bindings = null;
        Assert.Empty(ModelAuthoring.RemoveControl(model, "pin"));

        // Detaching frees the control, and parts attached to a deleted control leave with it.
        arm.Wave = null;
        Assert.Null(arm.Dependency("ripple"));
        Assert.Empty(ModelAuthoring.RemoveControl(model, "ripple"));
        model.Controls.Add(new() { Id = "tail", Parent = Humanoid.Hips });
        var tuft = ModelAuthoring.AddPart(model, new Ellipse2D { Center = Vector2.Zero, Radii = new(.1f, .1f) }.Validated(), "tail");
        model.Validate();
        var removed = ModelAuthoring.RemoveControl(model, "tail");
        Assert.Equal([tuft.Id], removed.Select(p => p.Id));
        Assert.DoesNotContain(model.Parts, p => p.Id == tuft.Id);
    }

    [Fact]
    public void ThePrototypePuppetRemovesDependentPartsThroughTheSameAnswers()
    {
        var puppet = PuppetTemplates.StickFigure();
        puppet.RemoveControl(Humanoid.Head);
        Assert.DoesNotContain(puppet.Parts, p => p.Id == Humanoid.Head);
        puppet.Validate();
    }

    [Fact]
    public void AGeometrySwitchDropsOnlyThePayloadsTheNewGeometryCannotCarry()
    {
        var model = Person;
        var head = model.Parts.Single(p => p.Id == Humanoid.Head);
        Assert.Empty(head.Payloads);
        ModelAuthoring.SetPartGeometry(model, Humanoid.Head, new LineSegmentCurve2D { Start = Vector2.Zero, End = Vector2.UnitY }.Validated());
        Assert.Empty(head.Payloads);
        head.Wave = CurveWave.Starter;
        model.Validate();
        Assert.Equal(["wave"], head.Payloads.Select(p => p.Name));
        ModelAuthoring.SetPartGeometry(model, Humanoid.Head, new Ellipse2D { Center = Vector2.Zero, Radii = new(.3f, .3f) }.Validated());
        Assert.Empty(head.Payloads);

        // A payload on the wrong geometry is refused by the payload contract, naming the requirement.
        head.Wave = CurveWave.Starter;
        Assert.Contains("wave payload requires a linked curve", Assert.Throws<InvalidDataException>(model.Validate).Message);
    }

    [Fact]
    public void ThePropertyRegistryDiffsAPartEditIntoTypedOverrides()
    {
        var model = Person;
        var head = model.Parts.Single(p => p.Id == Humanoid.Head);
        var edited = head with { ScaleX = 2, Hidden = true, OffsetX = -.1f };
        var changes = RigProperties.PartChanges(head, edited).ToList();
        Assert.Equal([RigProperties.ScaleX, RigProperties.OffsetX, RigProperties.Hidden], changes.Select(c => c.Target.Property));
        Assert.All(changes, c => Assert.Equal(Humanoid.Head, c.Target.Part));
        Assert.Equal(-.1f, changes[1].Value);
        Assert.Empty(RigProperties.PartChanges(head, head with { }));

        var variant = new ModelVariant { Id = "v", Name = "V", Base = model.Id };
        ModelAuthoring.EditPartOverride(variant, model, head, p => { p.ScaleY = 1.5f; p.Hidden = true; });
        Assert.Equal(1.5f, variant.Override(PropertyRef.OfPart(Humanoid.Head, RigProperties.ScaleY))!.Value);
        Assert.True(variant.Override(PropertyRef.OfPart(Humanoid.Head, RigProperties.Hidden))!.Flag);
        Assert.Equal(2, variant.Overrides.Count);
    }

    [Fact]
    public void TheLegacySourceFieldsSitBehindOneBoundaryWhoseRemainingInputsAreListed()
    {
        Assert.All(LegacySourceFields.Entries, e => Assert.Contains(e.Reader, new[] { "LegacyModelReader", "LegacyVariantReader", "LegacyClipReader" }));
        Assert.Contains(LegacySourceFields.Entries, e => e.Fields.Contains("waveAmplitude") && e.Replacement.Contains("CurveWave"));
        Assert.Contains(LegacySourceFields.Entries, e => e.Fields.Contains("faceX") && e.Replacement.Contains("refused"));
        Assert.Contains(LegacySourceFields.Entries, e => e.Asset == "clip" && e.Fields.Contains("faces") && e.Replacement.Contains("plays"));
        Assert.Contains(LegacySourceFields.Entries, e => e.Fields.Contains("chains"));
        Assert.Contains(LegacySourceFields.Entries, e => e.Fields.Contains("build"));
        Assert.Contains(LegacySourceFields.Entries, e => e.Asset == "variant" && e.Fields.Contains("parts"));
        Assert.Contains("version 11", LegacySourceFields.DeletionCondition);
        Assert.Contains("version 4", LegacySourceFields.DeletionCondition);

        // The conversion report the deletion condition waits on: exactly these shipped files still read through the boundary
        // (P7c converted person.json, the three remaining version-4 variants, the 28 face-track clips and, with `--convert-clips`,
        // the other 63 version-3 clips, whose only change is the version and the empty face list; P10a published quadruped, triceratops and pterosaur at 11; the three version-5 models read unchanged).
        // P11a converted the last three version-5 models: no shipped file reads through the boundary any more.
        Assert.Empty(LegacySourceFields.Remaining(TestModels.AuthoredRoot));

        // The boundary is what the parsers call: a current file passes through untouched, a legacy one comes out current.
        var person = Person.ToJson();
        Assert.Equal(person, LegacySourceFields.ReadModel(person));
        var legacy = File.ReadAllText(Path.Combine(TestModels.AuthoredRoot, "models", "person.json"));
        Assert.Contains($"\"version\": {CharacterModel.CurrentVersion}", LegacySourceFields.ReadModel(legacy).Replace(" ", "").Replace($"\"version\":{CharacterModel.CurrentVersion}", $"\"version\": {CharacterModel.CurrentVersion}"));
        var variant = new ModelVariant { Id = "v", Name = "V", Base = "person" }.ToJson();
        Assert.Equal(variant, LegacySourceFields.ReadVariant(variant));
        // The last version-4 variants converted with the face bundle (P7c); no variant or clip file reads through the boundary any more.
        var old = File.ReadAllText(Path.Combine(TestModels.AuthoredRoot, "variants", "club-caveman-build.json"));
        Assert.Equal(ModelVariant.CurrentVersion, ModelVariant.FromJson(old).Version);
        Assert.Contains($"\"version\": {ModelVariant.CurrentVersion}", old);
        Assert.All(LegacySourceFields.Remaining(TestModels.AuthoredRoot), f => Assert.Equal("model", f.Kind));
    }
}
