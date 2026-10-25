using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using System.Numerics;

namespace App2d.Tests.Authored;

/// <summary>P6a of the character-system migration: typed recipe overrides on variants with stable IDs, provenance, reset and explicit clear.</summary>
public sealed class VariantOverrideTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "variant-overrides-" + Guid.NewGuid().ToString("N"));

    public VariantOverrideTests()
    {
        foreach (var file in Directory.EnumerateFiles(TestModels.AuthoredRoot, "*.json", SearchOption.AllDirectories))
        {
            var target = Path.Combine(_root, Path.GetRelativePath(TestModels.AuthoredRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    public void Dispose() => Directory.Delete(_root, true);

    private static PropertyRef Forearm => PropertyRef.OfControl(Humanoid.LeftElbow, RigProperties.Length);

    [Fact]
    public void LengtheningAForearmAndSwappingAGloveLeavesTheBaseAloneAndResetRestoresInheritance()
    {
        var person = PersonTemplate.Model();
        var glove = PartAssetTests.Glove();
        var mitten = PartAssetTests.Glove("mitten", "#333333");
        Func<string, PartAsset?> library = id => new[] { glove, mitten }.FirstOrDefault(p => p.Id == id);
        ModelAuthoring.AddAttachment(person, "glove", Humanoid.LeftHandSocket, library, "left-glove");
        var baseJson = person.ToJson();
        var tall = PersonBuild.TallThin.Apply(person, "tall", "Tall");
        var broad = PersonBuild.ShortBroad.Apply(person, "broad", "Broad");
        var tallBefore = ResolvedModel.From(person, tall, library);
        var forearmBefore = tallBefore.Nodes[Humanoid.LeftElbow].Length;
        var handBefore = tallBefore.Rest[Humanoid.LeftHand];

        // The gate: a longer forearm and another glove on tall-thin alone.
        ModelAuthoring.ResizeBone(tallBefore, tall, Humanoid.LeftElbow, forearmBefore + .2f);
        ModelAuthoring.EditAttachmentOverride(tall, tallBefore, "left-glove", i => i.Asset = "mitten");
        var tallAfter = ResolvedModel.From(person, tall, library);
        Assert.Equal(baseJson, person.ToJson());
        Assert.Equal(forearmBefore + .2f, tallAfter.Nodes[Humanoid.LeftElbow].Length, 5);
        Assert.Equal(.2f, Vector3.Distance(handBefore, tallAfter.Rest[Humanoid.LeftHand]), 4);
        Assert.Same(mitten, tallAfter.Attachments.Single().Asset);
        Assert.Same(glove, ResolvedModel.From(person, broad, library).Attachments.Single().Asset);
        Assert.Equal(ResolvedModel.From(person, broad, library).Nodes[Humanoid.LeftElbow].Length, ResolvedModel.From(person, PersonBuild.ShortBroad.Apply(person, "b2", "B2"), library).Nodes[Humanoid.LeftElbow].Length);
        Assert.Equal(PropertySource.Override, tallAfter.Provenance(Forearm));
        Assert.True(tallAfter.Masks(Forearm), "the length override masks the arms binding");
        Assert.Equal(PropertySource.ParameterBinding, tallAfter.Provenance(PropertyRef.OfControl(Humanoid.LeftShoulder, RigProperties.Length)));
        Assert.Equal(PropertySource.Base, tallAfter.Provenance(PropertyRef.OfControl(Humanoid.Chest, RigProperties.LocalRotation)));

        // An unoverridden base edit propagates: the upper arm's authored length.
        var shoulder = person.Controls.Single(c => c.Id == Humanoid.LeftShoulder);
        shoulder.Length += .1f;
        var propagated = ResolvedModel.From(person, tall, library);
        Assert.Equal(tallAfter.Nodes[Humanoid.LeftShoulder].Length + .1f * PersonBuild.TallThin.Arms, propagated.Nodes[Humanoid.LeftShoulder].Length, 4);
        Assert.Equal(forearmBefore + .2f, propagated.Nodes[Humanoid.LeftElbow].Length, 5);

        // Reset removes the leaf override; nothing is written in its place and the binding is in force again.
        Assert.True(ModelAuthoring.ResetOverride(tall, Forearm));
        var reset = ResolvedModel.From(person, tall, library);
        Assert.Equal(forearmBefore, reset.Nodes[Humanoid.LeftElbow].Length, 5);
        Assert.Equal(PropertySource.ParameterBinding, reset.Provenance(Forearm));
        Assert.DoesNotContain(tall.Overrides, o => o.Target.Control is not null);
        ModelAuthoring.ResetOverride(tall, PropertyRef.OfAttachment("left-glove", RigProperties.Asset));
        Assert.Empty(tall.Overrides);
        Assert.Same(glove, ResolvedModel.From(person, tall, library).Attachments.Single().Asset);
    }

    [Fact]
    public void OverridesRoundTripAtVersionSixAndAreValidatedByName()
    {
        var person = PersonTemplate.Model();
        var variant = PersonBuild.TallThin.Apply(person, "tall", "Tall", "#d9e2ef");
        variant.Set(PropertyOverride.Of(Forearm, .4f));
        variant.Set(PropertyOverride.Of(PropertyRef.OfPart(Humanoid.Head, RigProperties.Hidden), false));
        variant.Set(PropertyOverride.Cleared(PropertyRef.OfControl(Humanoid.LeftShoulder, RigProperties.Length)));
        variant.Set(PropertyOverride.Of(PropertyRef.OfRole("walk"), "person-heavy-walk"));
        var json = variant.ToJson();
        Assert.Contains($"\"version\": {ModelVariant.CurrentVersion}", json);
        Assert.Contains("\"overrides\"", json);
        Assert.DoesNotContain("\"rest\"", json);
        Assert.DoesNotContain("\"parts\"", json);
        var read = ModelVariant.FromJson(json);
        Assert.Equal(json, read.ToJson());
        Assert.Equal(5, read.Overrides.Count);
        Assert.True(read.Override(PropertyRef.OfControl(Humanoid.LeftShoulder, RigProperties.Length))!.Clear);
        var resolved = ResolvedModel.From(person, read);
        Assert.Equal(.4f, resolved.Nodes[Humanoid.LeftElbow].Length);
        // A cleared length is the authored one: the arms binding is bypassed, so it equals the standard person's.
        Assert.Equal(ResolvedModel.From(person).Nodes[Humanoid.LeftShoulder].Length, resolved.Nodes[Humanoid.LeftShoulder].Length);
        Assert.Equal(resolved.AuthoredNumber(PropertyRef.OfControl(Humanoid.LeftShoulder, RigProperties.Length)), resolved.Nodes[Humanoid.LeftShoulder].Length);
        Assert.Equal("#d9e2ef", resolved.Parts.Single(p => p.Id == Humanoid.Body).RenderMaterial.Fill);
        Assert.Equal("person-heavy-walk", resolved.RoleClips["walk"]);

        string Refusal(PropertyOverride change)
        {
            var odd = new ModelVariant { Id = "odd", Name = "Odd", Base = person.Id, Overrides = [change] };
            return Assert.Throws<InvalidDataException>(() => ResolvedModel.From(person, odd)).Message;
        }
        Assert.Contains("no control 'tail'", Refusal(PropertyOverride.Of(PropertyRef.OfControl("tail", RigProperties.LocalX), 1)));
        Assert.Contains("only a bone node exposes a length", Refusal(PropertyOverride.Of(PropertyRef.OfControl(Humanoid.RightElbow, RigProperties.Length), 1)));
        Assert.Contains("bind its link", Refusal(PropertyOverride.Of(PropertyRef.OfControl(Humanoid.LeftElbow, RigProperties.LocalX), 1)));
        Assert.Contains("takes a number", Refusal(PropertyOverride.Of(Forearm, "long")));
        Assert.Contains("takes a flag", Refusal(PropertyOverride.Of(PropertyRef.OfPart(Humanoid.Head, RigProperties.Hidden), 2)));
        Assert.Contains("no part property 'face'", Refusal(PropertyOverride.Of(PropertyRef.OfPart(Humanoid.Head, "face"), "grumpy")));
        Assert.Contains("no part property 'girth'", Refusal(PropertyOverride.Of(PropertyRef.OfPart(Humanoid.Head, "girth"), 2)));
        Assert.Contains("cannot be cleared", Refusal(PropertyOverride.Cleared(PropertyRef.OfPart(Humanoid.Head, RigProperties.Material))));
        Assert.Contains("assigns role 'fly'", Refusal(PropertyOverride.Of(PropertyRef.OfRole("fly"), "person-walk")));
        Assert.Contains("between", Refusal(PropertyOverride.Of(PropertyRef.OfPart(Humanoid.Head, RigProperties.OffsetX), 500)));
        Assert.Contains("exactly one of value", Assert.Throws<InvalidDataException>(() => variant.Set(new() { Target = Forearm, Value = 1, Flag = true })).Message);
        var twice = new ModelVariant { Id = "twice", Name = "Twice", Base = person.Id, Overrides = [PropertyOverride.Of(Forearm, 1), PropertyOverride.Of(Forearm, 2)] };
        Assert.Contains("overridden twice", Assert.Throws<InvalidDataException>(twice.Validate).Message);
        // A binding cannot target a non-number property or a role.
        person.Parameters[0].Bindings.Add(new() { Target = PropertyRef.OfPart(Humanoid.Head, RigProperties.Material) });
        Assert.Contains("only number properties are bindable", Assert.Throws<InvalidDataException>(person.Validate).Message);
        person.Parameters[0].Bindings[^1] = new() { Target = PropertyRef.OfRole("walk") };
        Assert.Contains("not bindable", Assert.Throws<InvalidDataException>(person.Validate).Message);
    }

    [Fact]
    public void ALegacyFileReadsAsTypedOverridesAndRestPinsConvertWithTheBase()
    {
        var person = PersonTemplate.Model();
        const string legacy = """
            {"format":"app2d-variant","version":4,"id":"old","name":"Old","base":"person","build":{"legs":1.2},"rest":{},
             "parts":{"body":{"material":{"fill":"#d9e2ef"},"scaleX":1.2},"head":{"hidden":false}}}
            """;
        Assert.Equal(4, LegacyVariantReader.LegacyVersion(legacy));
        var read = ModelVariant.FromJson(legacy);
        Assert.Equal(ModelVariant.CurrentVersion, read.Version);
        Assert.Equal(1.2f, read.Parameters["legs"]);
        Assert.Equal(3, read.Overrides.Count);
        Assert.Equal("#d9e2ef", read.Override(PropertyRef.OfPart(Humanoid.Body, RigProperties.Material))!.Material!.Fill);
        Assert.Equal(1.2f, read.Override(PropertyRef.OfPart(Humanoid.Body, RigProperties.ScaleX))!.Value);
        Assert.False(read.Override(PropertyRef.OfPart(Humanoid.Head, RigProperties.Hidden))!.Flag);
        Assert.False(ResolvedModel.From(person, read).Parts.Single(p => p.Id == Humanoid.Head).Hidden);
        Assert.Null(LegacyVariantReader.LegacyVersion(read.ToJson()));
        // A legacy face word on a part (the retired payload, P7c) maps to its face instance only through the bundle conversion; the reader refuses it by name.
        var faced = legacy.Replace("\"hidden\":false", "\"face\":\"angry\",\"hidden\":false");
        Assert.Contains("convert the bundle", Assert.Throws<InvalidDataException>(() => ModelVariant.FromJson(faced)).Message);
        var (mapped, lines) = LegacyVariantReader.Convert(faced, null, new Dictionary<string, string> { [Humanoid.Head] = PersonTemplate.FaceInstance });
        Assert.Equal("angry", ModelVariant.FromJson(mapped).Override(PropertyRef.OfAttachment(PersonTemplate.FaceInstance, RigProperties.Expression))!.Selection);
        Assert.Contains(lines, l => l.Contains("face 'angry' -> attachment 'face' expression"));

        // A rest pin needs the base: the reader refuses it by name, the conversion converts it to setup overrides.
        var pinned = legacy.Replace("\"rest\":{}", "\"rest\":{\"head\":{\"x\":0,\"y\":3}}");
        Assert.Contains("VariantConversion", Assert.Throws<InvalidDataException>(() => ModelVariant.FromJson(pinned)).Message);
        var (converted, report) = LegacyVariantReader.Convert(pinned, new Dictionary<string, PuppetPoint> { ["head"] = new(0, 3) });
        var variant = ModelVariant.FromJson(converted);
        Assert.Contains(report, line => line.Contains("rest: 1 pin(s)"));
        VariantConversion.PinRest(person, variant, new Dictionary<string, PuppetPoint> { ["head"] = new(0, 3) });
        Assert.Equal(3, ResolvedModel.From(person, variant).Rest[Humanoid.Head].Y, 5);
        Assert.True(variant.HasOverride(PropertyRef.OfControl(Humanoid.Head, RigProperties.LocalY)));
    }

    [Fact]
    public void MovingAControlWritesLocalSetupOverridesNeverFrozenAbsolutePositions()
    {
        var person = PersonTemplate.Model();
        var variant = PersonBuild.TallThin.Apply(person, "tall", "Tall");
        var resolved = ResolvedModel.From(person, variant);
        var chest = resolved.Rest[Humanoid.Chest];
        var head = resolved.Rest[Humanoid.Head];
        // With children: one override on the chest; the head and arms ride along through the hierarchy.
        ModelAuthoring.MoveRest(resolved, variant, Humanoid.Chest, chest + new Vector3(.1f, .2f, 0), children: true);
        Assert.Equal(2, variant.Overrides.Count);
        Assert.All(variant.Overrides, o => Assert.Equal(Humanoid.Chest, o.Target.Control));
        var moved = ResolvedModel.From(person, variant);
        TestModels.Near(head + new Vector3(.1f, .2f, 0), moved.Rest[Humanoid.Head], 1e-5f);
        // The legs parameter still scales the hips beneath, since the chest's override is parent-local.
        variant.Parameters["legs"] = 1.4f;
        var longer = ResolvedModel.From(person, variant);
        Assert.True(longer.Rest[Humanoid.Chest].Y > moved.Rest[Humanoid.Chest].Y + .05f, "the chest rides on the lengthened legs");
        variant.Parameters["legs"] = PersonBuild.TallThin.Legs;
        variant.Overrides.Clear();
        // Without children: the chest moves, every unbound child gets its own setup override and stays exactly where it was.
        ModelAuthoring.MoveRest(resolved, variant, Humanoid.Chest, chest + new Vector3(.1f, .2f, 0), children: false);
        var kept = ResolvedModel.From(person, variant);
        TestModels.Near(chest + new Vector3(.1f, .2f, 0), kept.Rest[Humanoid.Chest], 1e-5f);
        foreach (var child in person.Controls.Where(c => c.Parent == Humanoid.Chest))
        {
            TestModels.Near(resolved.Rest[child.Id], kept.Rest[child.Id], 1e-5f, child.Id);
            Assert.True(variant.OverridesOf(PropertyRef.ControlKind, child.Id).Any(), child.Id + " was re-expressed");
        }
        TestModels.Near(resolved.Rest[Humanoid.LeftHand], kept.Rest[Humanoid.LeftHand], 1e-5f);
        // A bone tip drag on a variant overrides rotation and length; a point control's tip is refused with the reason.
        variant.Overrides.Clear();
        var target = new Vector2(resolved.Rest[Humanoid.LeftElbow].X - .3f, resolved.Rest[Humanoid.LeftElbow].Y + .1f);
        ModelAuthoring.MoveBoneTip(resolved, variant, Humanoid.LeftElbow, target);
        var dragged = ResolvedModel.From(person, variant);
        TestModels.Near(new(target, resolved.Rest[Humanoid.LeftHand].Z), dragged.Rest[Humanoid.LeftHand], 1e-4f);
        Assert.True(variant.HasOverride(PropertyRef.OfControl(Humanoid.LeftElbow, RigProperties.LocalRotation)));
        Assert.Contains("only drag a bone node", Assert.Throws<InvalidOperationException>(() => ModelAuthoring.MoveBoneTip(resolved, variant, Humanoid.RightElbow, target)).Message);
        Assert.Contains("legacy", Assert.Throws<InvalidOperationException>(() => ModelAuthoring.MoveRest(ResolvedModel.From(TestModels.Wheel().Model), new() { Id = "w", Name = "W", Base = "wheel" }, "wheel-root", Vector3.Zero, true)).Message);
    }

    [Fact]
    public void RoleOverridesReachTheEntityDuplicatesAreSiblingsAndIndependenceFlattensTheRecipe()
    {
        var session = new EditorSession(AuthoringWorkspace.Open(_root));
        session.Open("tall-thin");
        var workspace = session.Assets;
        var tall = session.SubjectVariant!;
        var person = workspace.Model("person")!;
        Assert.True(session.Edit(tall, () => ModelAuthoring.SetOverride(tall.Asset, person.Asset, PropertyOverride.Of(PropertyRef.OfRole("walk"), "person-heavy-walk"))), session.Message);
        var guard = workspace.CompileEntity("spear-guard")!;
        Assert.Equal("person-heavy-walk", guard.Roles["walk"].Clip.Id);
        Assert.Equal("variant:tall-thin", guard.Roles["walk"].Source);
        Assert.Contains(workspace.Uses("tall-thin"), r => r.Id == "person-heavy-walk" && r.Relation == "role walk (override)");
        Assert.Contains(workspace.UsedBy("person-heavy-walk"), r => r.Id == "tall-thin");
        Assert.Empty(workspace.Problems(tall));
        Assert.True(session.Edit(tall, () => ModelAuthoring.ResizeBone(workspace.Resolve("tall-thin")!, tall.Asset, Humanoid.LeftElbow, .5f)), session.Message);
        Assert.False(person.Dirty);
        Assert.Equal(.5f, workspace.Resolve("tall-thin")!.Nodes[Humanoid.LeftElbow].Length);
        Assert.NotEqual(.5f, workspace.Resolve("short-broad")!.Nodes[Humanoid.LeftElbow].Length);

        // A duplicate is a sibling of the same base with the same overrides; editing it leaves the source alone.
        Assert.True(session.DuplicateVariant("tall-thin", "tall-twin", "Tall twin"), session.Message);
        var twin = session.SubjectVariant!;
        Assert.Equal("person", twin.Asset.Base);
        Assert.Equal(.5f, workspace.Resolve("tall-twin")!.Nodes[Humanoid.LeftElbow].Length);
        Assert.True(session.Edit(twin, () => ModelAuthoring.ResetOverride(twin.Asset, Forearm)), session.Message);
        Assert.Equal(.5f, workspace.Resolve("tall-thin")!.Nodes[Humanoid.LeftElbow].Length);

        // Making it independent flattens the resolved recipe: lengths, the role binding and the overrides become authored.
        Assert.True(session.MakeIndependent("tall-thin", "tall-flat", "Tall flat"), session.Message);
        var flat = session.SubjectModel!;
        Assert.Equal(.5f, flat.Asset.Controls.Single(c => c.Id == Humanoid.LeftElbow).Length);
        Assert.Equal("person-heavy-walk", flat.Asset.MotionSets[0].Roles["walk"]);
        Assert.Empty(flat.Asset.Parameters);
        foreach (var control in workspace.Resolve("tall-thin")!.Order)
        {
            TestModels.Near(workspace.Resolve("tall-thin")!.Rest[control.Id], workspace.Resolve("tall-flat")!.Rest[control.Id], 1e-5f, control.Id);
        }

        // A role override naming a missing clip is a repairable problem, reported by name, not a silent drop.
        session.Open("tall-thin");
        Assert.True(session.Edit(tall, () => tall.Asset.Set(PropertyOverride.Of(PropertyRef.OfRole("walk"), "no-such-walk"))), session.Message);
        Assert.Contains(workspace.Problems(tall), p => p.Contains("no-such-walk"));
        session.Undo();
        Assert.Empty(workspace.Problems(tall));
        session.SaveAll();
        Assert.False(session.MessageIsError, session.Message);
        var reopened = AuthoringWorkspace.Open(_root);
        Assert.Empty(reopened.LoadErrors);
        Assert.Equal(.5f, reopened.Resolve("tall-thin")!.Nodes[Humanoid.LeftElbow].Length);
        Assert.Equal("person-heavy-walk", reopened.CompileEntity("spear-guard")!.Roles["walk"].Clip.Id);
        Assert.Contains($"\"version\": {ModelVariant.CurrentVersion}", File.ReadAllText(Path.Combine(_root, "variants", "tall-thin.json")));
    }

    [Fact]
    public void LooksWriteTypedOverridesAndMergeOverAnExistingMaterial()
    {
        var person = PersonTemplate.Model(face: true);
        var variant = PersonBuild.TallThin.Apply(person, "tall", "Tall", "#d9e2ef");
        ModelAuthoring.EditPartOverride(variant, person, ResolvedModel.From(person, variant, PersonTemplate.PartOf).Parts.Single(p => p.Id == Humanoid.Body),
            p => p.Material = p.RenderMaterial with { Outline = (p.RenderMaterial.Outline ?? new()) with { Width = .09f } });
        EntityAuthoring.ApplyLook(person, variant, "ember");
        var body = variant.Override(PropertyRef.OfPart(Humanoid.Body, RigProperties.Material))!.Material!;
        Assert.Equal("#eab596", body.Fill);
        Assert.Equal(.09f, body.Outline!.Width);
        Assert.Equal("determined", variant.Override(PropertyRef.OfAttachment(PersonTemplate.FaceInstance, RigProperties.Expression))!.Selection);
        var look = EntityAuthoring.SaveLook(person, variant, "mine", "Mine");
        Assert.Equal("#eab596", look.Parts[Humanoid.Body].Material!.Fill);
        Assert.Equal("determined", look.Expressions[PersonTemplate.FaceInstance]);
        Assert.DoesNotContain(look.Parts.Keys, part => part == Humanoid.LeftHand);
    }
}
