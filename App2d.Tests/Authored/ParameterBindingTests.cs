using App2d.CharacterSeeds;
using App2d.CharacterStudio.Editing;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Mathematics.Transforms;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json.Nodes;

namespace App2d.Tests.Authored;

/// <summary>P4a of the character-system migration: declared parameters, their simple bindings and the resolution order.</summary>
public sealed class ParameterBindingTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);

    /// <summary>A crank whose hub length is a parameter: the pin at its tip, a lamp at its midpoint, a disc fitted to it.</summary>
    private static CharacterModel Crank(params ParameterBinding[] extra) => new()
    {
        Id = "crank",
        Name = "Crank",
        Controls =
        [
            new() { Id = "frame", Rest = new(0, 1) },
            new() { Id = "hub", Parent = "frame", Node = RigNodeKinds.Bone, Local = new() { X = .5f, Rotation = .3f }, Length = .4f },
            new() { Id = "pin", Parent = "hub", Node = RigNodeKinds.Bone, Link = new(), Length = .25f },
            new() { Id = "lamp", Parent = "hub", Node = RigNodeKinds.Bone, Link = new() { T = .5f } },
            new() { Id = "post", Parent = "frame", Rest = new(-.5f, 1.5f) },
        ],
        Parts = [new() { Id = "disc", A = "hub", Geometry = Ellipse2D.Builder.Create(new(.2f, .2f)), ScaleX = .5f, Material = new() { Fill = "#c9d3d8" } }],
        Parameters =
        [
            new()
            {
                Id = "reach", Name = "Reach", Units = ParameterUnits.Length, Default = .4f, Min = .1f, Max = 2, SoftMin = .2f, SoftMax = 1,
                Bindings = [new() { Kind = BindingKinds.Set, Target = PropertyRef.OfControl("hub", RigProperties.Length) }, .. extra],
            },
        ],
        Presets = [new() { Id = "long", Name = "Long", Values = { ["reach"] = 1 } }],
    };

    private static ModelVariant Values(string basis, params (string Id, float Value)[] values)
    {
        var variant = new ModelVariant { Id = basis + "-values", Name = "Values", Base = basis };
        foreach (var (id, value) in values)
        {
            variant.Parameters[id] = value;
        }

        return variant;
    }

    private static Vector2 XY(Vector3 v) => new(v.X, v.Y);

    [Fact]
    public void TheShippedPersonDeclaresItsProportionsAsParametersWithBindings()
    {
        var person = Catalog.Models["person"];
        Assert.Equal(CharacterModel.CurrentVersion, person.Version);
        Assert.Equal(PersonBuild.Ids, person.Parameters.Select(p => p.Id));
        Assert.All(person.Parameters, p => Assert.Equal(ParameterUnits.Factor, p.Units));
        Assert.Equal(["standard", "tall-thin", "short-broad", "stilted", "squat"], person.Presets.Select(p => p.Id));
        Assert.Equal(PersonBuild.TallThin.Values(), person.Presets[1].Values);
        var resolved = Catalog.Resolve("person");
        Assert.Equal("arms", resolved.Parameters.BindingOfControl(Humanoid.LeftUpperArmBone, RigProperties.Length)!.Value.Parameter.Id);
        Assert.Equal("arms", resolved.Parameters.BindingOfControl("right-elbow", RigProperties.Length)!.Value.Parameter.Id);
        Assert.Equal("width", resolved.Parameters.BindingOfPart("body", RigProperties.ScaleX)!.Value.Parameter.Id);
        Assert.Equal("head", resolved.Parameters.BindingOfPart("head", RigProperties.ScaleY)!.Value.Parameter.Id);
        var hips = resolved.Parameters.BindingOfControl("hips", RigProperties.LocalY)!.Value;
        Assert.Equal("legs", hips.Parameter.Id);
        Assert.Equal(resolved.Rest["left-foot"].Y, hips.Binding.About, 5);
        Assert.Null(resolved.Parameters.BindingOfControl("chest", RigProperties.LocalX + "x"));
        // The seed (with its face instance, P7c) declares the same thing the file carries, and a default resolution leaves every authored rest untouched.
        var seed = PersonTemplate.Model(face: true);
        Assert.Equal(person.Parameters.Select(p => (p.Id, p.Bindings.Count)), seed.Parameters.Select(p => (p.Id, p.Bindings.Count)));
        // A point control nobody moved keeps its authored rest; the hips' ground-anchored binding writes them at the default (float roundoff).
        foreach (var control in person.Controls.Where(c => c.Node == RigNodeKinds.Point))
        {
            TestModels.Near(control.Rest.XYZ, resolved.Rest[control.Id], 1e-6f, control.Id);
        }
    }

    public static TheoryData<string> Variants => [.. Catalog.Variants.Keys];

    [Theory, MemberData(nameof(Variants))]
    public void PersonVariantsResolveExactlyAsTheRetiredBuildFormulasDid(string id)
    {
        var person = Catalog.Models["person"];
        var variant = Catalog.Variants[id];
        var standard = Catalog.Resolve("person");
        var actual = ResolvedModel.From(person, variant, Catalog.Parts.GetValueOrDefault);
        var (rest, parts) = LegacyPersonBuild(standard, PersonBuild.From(variant.Parameters));
        foreach (var control in person.Controls)
        {
            TestModels.Near(rest[control.Id], actual.Rest[control.Id], 1e-5f, id + "/" + control.Id);
        }

        foreach (var part in new[] { "body", "head" })
        {
            var expected = parts.Single(p => p.Id == part);
            var got = actual.Parts.Single(p => p.Id == part);
            Assert.Equal(expected.ScaleX, got.ScaleX, 5);
            Assert.Equal(expected.ScaleY, got.ScaleY, 5);
            Assert.Equal(expected.OffsetY, got.OffsetY, 5);
        }
        Assert.Equal(variant.Parameters.GetValueOrDefault("arms", 1), actual.Nodes[Humanoid.LeftUpperArmBone].Length / standard.Nodes[Humanoid.LeftUpperArmBone].Length, 4);
    }

    [Fact]
    public void ArbitraryPersonValuesMatchTheRetiredFormulasToo()
    {
        var person = Catalog.Models["person"];
        var standard = Catalog.Resolve("person");
        var random = new Random(7);
        for (var i = 0; i < 12; i++)
        {
            var build = new PersonBuild
            {
                Legs = random.NextSingle() * 1.4f + .5f,
                Torso = random.NextSingle() * 1.4f + .5f,
                Arms = random.NextSingle() * 1.4f + .5f,
                Width = random.NextSingle() * 1.4f + .5f,
                Head = random.NextSingle() * 1.4f + .5f,
            };
            var actual = ResolvedModel.From(person, build.Apply(person, "random", "Random"), Catalog.Parts.GetValueOrDefault);
            var (rest, _) = LegacyPersonBuild(standard, build);
            foreach (var control in person.Controls)
            {
                TestModels.Near(rest[control.Id], actual.Rest[control.Id], 1e-5f, $"{i}/{control.Id}");
            }
        }
    }

    /// <summary>The retired <c>PersonBuildRule.Apply</c>, kept here as the oracle the data bindings must reproduce.</summary>
    private static (Dictionary<string, Vector3> Positions, List<PartGeometry> Parts) LegacyPersonBuild(ResolvedModel standard, PersonBuild b)
    {
        var rest = standard.Order.ToDictionary(c => c.Id, c => standard.Rest[c.Id], StringComparer.Ordinal);
        var parts = standard.Base.Parts.ConvertAll(p => p with { });
        var built = new Dictionary<string, Vector3>(StringComparer.Ordinal);
        var ground = rest[Humanoid.LeftFoot].Y;
        float Lift(float y) => ground + (y - ground) * b.Legs;
        Vector3 Across(Vector3 v) => new(v.X * b.Width, v.Y, v.Z * b.Width);
        built[Humanoid.Hips] = rest[Humanoid.Hips] with { Y = Lift(rest[Humanoid.Hips].Y) };
        built[Humanoid.Chest] = built[Humanoid.Hips] + (rest[Humanoid.Chest] - rest[Humanoid.Hips]) * b.Torso;
        built[Humanoid.Head] = built[Humanoid.Chest] + (rest[Humanoid.Head] - rest[Humanoid.Chest]) * b.Torso;
        foreach (var (hip, knee, foot, shoulderId, elbow, hand) in new[]
        {
            (Humanoid.LeftHip, Humanoid.LeftKnee, Humanoid.LeftFoot, Humanoid.LeftShoulder, Humanoid.LeftElbow, Humanoid.LeftHand),
            (Humanoid.RightHip, Humanoid.RightKnee, Humanoid.RightFoot, Humanoid.RightShoulder, Humanoid.RightElbow, Humanoid.RightHand)
        })
        {
            built[foot] = Across(rest[foot]);
            built[hip] = Across(rest[hip]) with { Y = Lift(rest[hip].Y) };
            built[knee] = built[foot] + (rest[knee] - rest[foot]) * b.Legs;
            var shoulder = rest[shoulderId] - rest[Humanoid.Chest];
            built[shoulderId] = built[Humanoid.Chest] + new Vector3(shoulder.X * b.Width, shoulder.Y * b.Torso, shoulder.Z * b.Width);
            built[elbow] = built[shoulderId] + (rest[elbow] - rest[shoulderId]) * b.Arms;
            built[hand] = built[elbow] + (rest[hand] - rest[elbow]) * b.Arms;
            // P11a: the solved upper-arm bone sits at its shoulder pivot.
            built[shoulderId == Humanoid.LeftShoulder ? Humanoid.LeftUpperArmBone : Humanoid.RightUpperArmBone] = built[shoulderId];
        }
        foreach (var (control, point) in built)
        {
            rest[control] = point;
        }

        var body = parts.FindIndex(p => p.Id == Humanoid.Body);
        var head = parts.FindIndex(p => p.Id == Humanoid.Head);
        parts[body] = parts[body] with { ScaleX = parts[body].ScaleX * b.Width, ScaleY = parts[body].ScaleY * b.Torso, OffsetY = parts[body].OffsetY * b.Torso };
        parts[head] = parts[head] with { ScaleX = parts[head].ScaleX * b.Head, ScaleY = parts[head].ScaleY * b.Head };
        return (rest, parts);
    }

    [Fact]
    public void ABoundLengthMovesTheMidpointChildAndTheTipWithIt()
    {
        var model = Crank();
        var standard = ResolvedModel.From(model);
        var longer = ResolvedModel.From(model, Values("crank", ("reach", 1)));
        Assert.Equal(.4f, standard.Nodes["hub"].Length);
        Assert.Equal(1, longer.Nodes["hub"].Length);
        Assert.Equal(1, longer.ParameterValues["reach"]);
        var hub = XY(longer.Rest["hub"]);
        TestModels.Near(new(hub + Rotation2D.Apply(new(.5f, 0), .3f), 0), longer.Rest["lamp"]);
        TestModels.Near(new(hub + Rotation2D.Apply(new(1, 0), .3f), 0), longer.Rest["pin"]);
        Assert.Equal(1, new Frame2D(longer.RestTransforms["lamp"]).XAxis.Length(), 5);
        // The preset writes the same value through the authoring route.
        var fromPreset = new ModelVariant { Id = "long", Name = "Long", Base = "crank" };
        ModelAuthoring.ApplyPreset(model, fromPreset, "long");
        Assert.Equal(1, fromPreset.Parameters["reach"]);
        TestModels.Near(longer.Rest["lamp"], ResolvedModel.From(model, fromPreset).Rest["lamp"]);
        // The same turn clip plays on both reaches: the lamp stays at the midpoint while the hub turns.
        var clip = ClipAuthoring.New(standard, "turn", "Turn");
        ClipAuthoring.SetKey(clip, new(MotionClip.RotateKind, "hub"), 0, default, 1.2f);
        clip.Validate(standard);
        clip.Validate(longer);
        var pose = PoseEvaluator.Sample(longer, clip, 0);
        TestModels.Near(new(XY(pose.World("hub")) + Rotation2D.Apply(new(.5f, 0), 1.5f), 0), pose.World("lamp"));
    }

    [Fact]
    public void ScaleMapAndSetBindingsEvaluateAsDeclared()
    {
        var model = Crank(
            new() { Kind = BindingKinds.Scale, Target = PropertyRef.OfControl("pin", RigProperties.Length), Reference = PropertyRef.OfControl("hub", RigProperties.Length), About = .1f },
            new() { Kind = BindingKinds.Map, Target = PropertyRef.OfControl("lamp", RigProperties.LinkT), FromMin = .1f, FromMax = 2, ToMin = 0, ToMax = 1 },
            new() { Kind = BindingKinds.Scale, Target = PropertyRef.OfPart("disc", RigProperties.ScaleX) },
            new() { Kind = BindingKinds.Scale, Target = PropertyRef.OfControl("post", RigProperties.LocalY), About = 1 });
        model.Validate();
        var resolved = ResolvedModel.From(model, Values("crank", ("reach", .6f)));
        // pin.length = .1 + (hub.length - .1) * reach, reading the hub's final (bound) length.
        Assert.Equal(.1f + (.6f - .1f) * .6f, resolved.Nodes["pin"].Length, 5);
        // lamp.t maps reach's [.1, 2] onto [0, 1].
        Assert.Equal((.6f - .1f) / 1.9f, resolved.Nodes["lamp"].Link!.T, 5);
        // disc.scaleX = authored .5 * reach.
        Assert.Equal(.3f, resolved.Parts.Single(p => p.Id == "disc").ScaleX, 5);
        // A point control's layout is its parent-local offset (.5 above the frame at y = 1), scaled about the anchor 1: 1 + (.5 - 1) * .6.
        Assert.Equal(1 + (1 + (.5f - 1) * .6f), resolved.Rest["post"].Y, 5);
        Assert.Equal(-.5f, resolved.Rest["post"].X, 5);
        Assert.Equal(["hub", "pin", "lamp", "disc", "post"], resolved.Parameters.Bindings.Select(b => b.Binding.Target.Owner));
        var describe = resolved.Parameters.BindingOfControl("pin", RigProperties.Length)!.Value.Binding.Describe("reach");
        Assert.Contains("hub", describe);
        Assert.Contains("reach", describe);
    }

    [Fact]
    public void ExplicitOverridesWinOverBoundValues()
    {
        var model = Crank(
            new() { Kind = BindingKinds.Scale, Target = PropertyRef.OfPart("disc", RigProperties.ScaleX) },
            new() { Kind = BindingKinds.Scale, Target = PropertyRef.OfControl("post", RigProperties.LocalY) });
        var variant = Values("crank", ("reach", 1));
        ModelAuthoring.MoveRest(ResolvedModel.From(model, variant), variant, "post", new(-.5f, 2, 0), true);
        variant.Set(PropertyOverride.Of(PropertyRef.OfPart("disc", RigProperties.ScaleX), .2f));
        var resolved = ResolvedModel.From(model, variant);
        Assert.Equal(2, resolved.Rest["post"].Y, 5);
        Assert.Equal(.2f, resolved.Parts.Single(p => p.Id == "disc").ScaleX);
        Assert.True(resolved.Masks(PropertyRef.OfControl("post", RigProperties.LocalY)));
        Assert.Equal(PropertySource.Override, resolved.Provenance(PropertyRef.OfPart("disc", RigProperties.ScaleX)));
        // The bound value is still in force where no override names the property.
        Assert.Equal(1, resolved.Nodes["hub"].Length);
        Assert.Equal(PropertySource.ParameterBinding, resolved.Provenance(PropertyRef.OfControl("hub", RigProperties.Length)));
        // A clear pins the authored value, bypassing the binding, and stays live.
        variant.Set(PropertyOverride.Cleared(PropertyRef.OfPart("disc", RigProperties.ScaleX)));
        Assert.Equal(.5f, ResolvedModel.From(model, variant).Parts.Single(p => p.Id == "disc").ScaleX);
        Assert.Equal(PropertySource.Cleared, ResolvedModel.From(model, variant).Provenance(PropertyRef.OfPart("disc", RigProperties.ScaleX)));
        var person = Catalog.Models["person"];
        var tall = PersonBuild.TallThin.Apply(person, "tall", "Tall");
        var resolvedTall = ResolvedModel.From(person, tall, Catalog.Parts.GetValueOrDefault);
        ModelAuthoring.MoveRest(resolvedTall, tall, "head", new(0, 3, resolvedTall.Rest["head"].Z), true);
        Assert.Equal(3, ResolvedModel.From(person, tall, Catalog.Parts.GetValueOrDefault).Rest["head"].Y, 5);
    }

    [Theory]
    [InlineData("unknown-value", "has no such parameter")]
    [InlineData("out-of-range", "between 0.1 and 2")]
    [InlineData("bound-twice", "one binding")]
    [InlineData("length-on-point", "only a bone node exposes a length")]
    [InlineData("link-on-unlinked", "has no link")]
    [InlineData("translation-on-linked", "bind its link")]
    [InlineData("unknown-property", "no control property")]
    [InlineData("unknown-part", "no part 'hat'")]
    [InlineData("cycle", "form a cycle")]
    [InlineData("preset-unknown", "no parameter 'span'")]
    [InlineData("default-outside", "default must be between")]
    [InlineData("duplicate-id", "duplicate parameter")]
    [InlineData("bad-units", "unknown units")]
    [InlineData("no-parameters", "exposes no parameters")]
    public void DeclarationsAndValuesAreValidatedByName(string edit, string fragment)
    {
        var model = Crank();
        var variant = Values("crank", ("reach", .5f));
        var reach = model.Parameters[0];
        switch (edit)
        {
            case "unknown-value": variant.Parameters["span"] = 1; break;
            case "out-of-range": variant.Parameters["reach"] = 3; break;
            case "bound-twice": reach.Bindings.Add(new() { Target = PropertyRef.OfControl("hub", RigProperties.Length) }); break;
            case "length-on-point": reach.Bindings.Add(new() { Target = PropertyRef.OfControl("post", RigProperties.Length) }); break;
            case "link-on-unlinked": reach.Bindings.Add(new() { Target = PropertyRef.OfControl("hub", RigProperties.LinkT) }); break;
            case "translation-on-linked": reach.Bindings.Add(new() { Target = PropertyRef.OfControl("pin", RigProperties.LocalX) }); break;
            case "unknown-property": reach.Bindings.Add(new() { Target = PropertyRef.OfControl("hub", "girth") }); break;
            case "unknown-part": reach.Bindings.Add(new() { Target = PropertyRef.OfPart("hat", RigProperties.ScaleX) }); break;
            case "cycle":
                reach.Bindings.Clear();
                reach.Bindings.Add(new() { Kind = BindingKinds.Scale, Target = PropertyRef.OfControl("hub", RigProperties.Length), Reference = PropertyRef.OfControl("pin", RigProperties.Length) });
                reach.Bindings.Add(new() { Kind = BindingKinds.Scale, Target = PropertyRef.OfControl("pin", RigProperties.Length), Reference = PropertyRef.OfControl("hub", RigProperties.Length) });
                break;
            case "preset-unknown": model.Presets[0].Values["span"] = 1; break;
            case "default-outside": reach.Default = 5; break;
            case "duplicate-id": model.Parameters.Add(new() { Id = "reach", Max = 2 }); break;
            case "bad-units": reach.Units = "furlongs"; break;
            case "no-parameters": model.Parameters.Clear(); model.Presets.Clear(); break;
        }
        var message = Assert.Throws<InvalidDataException>(() => ResolvedModel.From(model, variant)).Message;
        Assert.Contains(fragment, message);
    }

    [Fact]
    public void ModelsRoundTripAtTheCurrentVersionAndAVersionSevenBuildRuleIsRefused()
    {
        var person = Catalog.Models["person"];
        var json = person.ToJson();
        Assert.Contains("\"parameters\"", json);
        Assert.Contains("\"about\": 0.025", json);
        Assert.DoesNotContain("\"build\"", json);
        Assert.Equal(json, CharacterModel.FromJson(json).ToJson());
        // The file on disk is at the current version (12, P11c) and is what the asset writes.
        var file = File.ReadAllText(Path.Combine(TestModels.AuthoredRoot, "models", "person.json"));
        Assert.Contains($"\"version\": {CharacterModel.CurrentVersion}", file);
        Assert.Equal(json, file);
        // A code proportion rule ('build' on a model) is a retired member: refused by name with the legacy readers gone (P11c).
        var crank = JsonNode.Parse(Crank().ToJson())!.AsObject();
        crank["build"] = "person";
        Assert.Contains("build", Assert.Throws<System.Text.Json.JsonException>(() => CharacterModel.FromJson(crank.ToJsonString())).Message);
        // A variant file keeps its 'build' member: the values of the base's declared parameters.
        var variant = Catalog.Variants["tall-thin"];
        Assert.Contains("\"build\"", variant.ToJson());
        Assert.Equal(1.2f, variant.Parameters["legs"]);
    }

    [Fact]
    public void AUniformPoseScaleChannelScalesACanonicalNodeAndCarriesItsBoundChildren()
    {
        var model = ResolvedModel.From(Crank());
        var clip = ClipAuthoring.New(model, "swell", "Swell");
        ClipAuthoring.SetKey(clip, new(MotionClip.ScaleKind, "hub"), 0, new(.5f, .5f, 0));
        clip.Validate(model);
        Assert.Equal(ChannelSemantics.UniformPoseScale, ChannelSemantics.OfTrack(MotionClip.ScaleKind));
        Assert.Equal(ChannelSemantics.ArtworkFit, ChannelSemantics.OfTrack(MotionClip.FitKind));
        Assert.Contains(ClipAuthoring.ChannelsForControl(model, "hub"), c => c.Kind == MotionClip.ScaleKind);
        Assert.DoesNotContain(ClipAuthoring.ChannelsForControl(model, "hub"), c => c.Kind == "shear");
        var pose = PoseEvaluator.Sample(model, clip, 0);
        var hub = XY(pose.World("hub"));
        Assert.Equal(.4f * 1.5f, Vector2.Distance(hub, XY(pose.World("pin"))), 4);
        Assert.Equal(.2f * 1.5f, Vector2.Distance(hub, XY(pose.World("lamp"))), 4);
        Assert.Equal(1.5f, new Frame2D(pose.Bones["hub"]).XAxis.Length(), 4);
        Assert.Equal(1.5f, new Frame2D(pose.Bones["hub"]).YAxis.Length(), 4);
        // The length is a dimension, not the scale: it is unchanged by the channel.
        Assert.Equal(.4f, model.Nodes["hub"].Length);
        var skewed = ClipAuthoring.New(model, "skew", "Skew");
        ClipAuthoring.SetKey(skewed, new(MotionClip.ScaleKind, "hub"), 0, new(.5f, 0, 0));
        Assert.Contains("uniform pose scale", Assert.Throws<InvalidDataException>(() => skewed.Validate(model)).Message);
        // Shear left the vocabulary with the affine adapter (P11c): a track of that kind is unknown by name.
        var sheared = ClipAuthoring.New(model, "shear", "Shear");
        sheared.Tracks.Add(new() { Kind = "shear", Target = "hub", Keys = [new() { Value = new(.2f, 0, 0) }] });
        Assert.Contains("kind", Assert.Throws<InvalidDataException>(() => sheared.Validate(model)).Message);
        Assert.DoesNotContain("shear", MotionClip.TrackKinds);
        Assert.Equal(9, ChannelSemantics.All.Count);
    }

    [Fact]
    public void FlatteningBakesTheParameterValuesAndDropsTheDeclarations()
    {
        var person = Catalog.Models["person"];
        var tall = ResolvedModel.From(person, Catalog.Variants["tall-thin"], Catalog.Parts.GetValueOrDefault);
        var flat = ResolvedModel.From(ModelAuthoring.Flatten(tall, "tall-base", "Tall base"), null, Catalog.Parts.GetValueOrDefault);
        Assert.Empty(flat.Base.Parameters);
        Assert.Empty(flat.Base.Presets);
        foreach (var control in person.Controls)
        {
            TestModels.Near(tall.Rest[control.Id], flat.Rest[control.Id], 1e-5f, control.Id);
        }

        Assert.Equal(tall.Parts.Single(p => p.Id == "body").ScaleX, flat.Parts.Single(p => p.Id == "body").ScaleX, 5);
        Assert.Equal(tall.Nodes[Humanoid.LeftUpperArmBone].Length, flat.Nodes[Humanoid.LeftUpperArmBone].Length, 5);
    }

    [Fact]
    public void ParameterAuthoringIsUndoableAndSurvivesSaveAndReopen()
    {
        var root = Path.Combine(Path.GetTempPath(), $"parameters-{Guid.NewGuid():N}");
        PersonTemplate.WriteStudies(root);
        TestModels.CopyModelTemplates(root);
        var session = new EditorSession(AuthoringWorkspace.Open(root));
        session.Open("person");
        var document = session.SubjectModel!;
        var before = document.Serialize();
        // Every Person layout property is already bound by a proportion; the hand bone's (zero) length is free.
        Assert.False(session.Edit(document, () => ModelAuthoring.AddBinding(document.Asset, "arms", new() { Target = PropertyRef.OfControl(Humanoid.Hips, RigProperties.LocalY) })));
        Assert.Contains("one binding", session.Message);
        Assert.True(session.Edit(document, () => ModelAuthoring.AddParameter(document.Asset, "hand-span", "Hand span", ParameterUnits.Length, 0, 0, 1)), session.Message);
        Assert.True(session.Edit(document, () => ModelAuthoring.AddBinding(document.Asset, "hand-span", new() { Kind = BindingKinds.Set, Target = PropertyRef.OfControl("left-hand", RigProperties.Length) })), session.Message);
        Assert.Equal(0, session.Assets.Resolve("person")!.Nodes["left-hand"].Length);
        session.Change(document, () => { document.Asset.Parameters[^1].Default = .12f; document.Asset.Validate(); });
        session.CommitAll();
        Assert.Equal(.12f, session.Assets.Resolve("person")!.Nodes["left-hand"].Length);
        Assert.True(session.Save(document), session.Message);
        var reopened = new EditorSession(AuthoringWorkspace.Open(root));
        var saved = reopened.Assets.Model("person")!.Asset;
        Assert.Equal(.12f, saved.Parameters.Single(p => p.Id == "hand-span").Default);
        Assert.Equal("hand-span", ResolvedModel.Rig(saved).Parameters.BindingOfControl("left-hand", RigProperties.Length)!.Value.Parameter.Id);
        Assert.Empty(reopened.Assets.Problems(reopened.Assets.Model("person")!));
        Directory.Delete(root, true);
        while (document.CanUndo)
        {
            session.Undo();
        }

        Assert.Equal(before, document.Serialize());
        Assert.True(session.Edit(document, () => ModelAuthoring.RemoveParameter(document.Asset, "legs")), session.Message);
        Assert.DoesNotContain(document.Asset.Presets[1].Values.Keys, k => k == "legs");
        Assert.Null(session.Assets.Resolve("person")!.Parameters.BindingOfControl("hips", RigProperties.LocalY));
        Assert.True(session.NewVariant("broad", "Broad", "person", "short-broad"), session.Message);
        Assert.Equal(PersonBuild.ShortBroad.Width, session.SubjectVariant!.Asset.Parameters["width"]);
        Assert.False(session.SubjectVariant.Asset.Parameters.ContainsKey("legs"));
    }
}
