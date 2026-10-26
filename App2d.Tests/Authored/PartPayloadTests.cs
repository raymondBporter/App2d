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
    public void AnOlderPartFileIsRefusedByVersion()
    {
        // P11c retired the legacy source boundary: a file below its kind's current version is refused by name, never translated.
        var json = PersonTemplate.Face.ToJson().Replace($"\"version\": {PartAsset.CurrentVersion}", "\"version\": 1");
        Assert.Contains("unsupported format/version", Assert.Throws<InvalidDataException>(() => PartAsset.FromJson(json)).Message);
    }
}
