using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Geometry;
using System.Numerics;
using System.Text.Json.Nodes;

namespace App2d.Tests.Authored;

/// <summary>
/// P7c of the character-system migration: the Person's procedural face converted onto the baked face part
/// <c>parts/face-person.json</c> instanced on the head socket, the 28 face-track clips onto its selection channel, the
/// variants' and looks' face words onto its expression, gameplay requests answered by the asset's bindings, and the fixed
/// face fields, tracks and vocabulary gone from the generic evaluator.
/// </summary>
public sealed class PersonFaceConversionTests : IDisposable
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
    private readonly string _root = Path.Combine(Path.GetTempPath(), "face-payload-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    [Fact]
    public void TheShippedPersonWearsTheBakedFacePartAndNothingCarriesAFaceFieldAnyMore()
    {
        Assert.Empty(Catalog.Errors);
        var person = Catalog.Resolve("person");
        var face = person.Attachment(PersonTemplate.FaceInstance);
        Assert.NotNull(face);
        Assert.True(face!.IsAnimated);
        Assert.Equal(ProceduralFace.AssetId, face.Asset.Id);
        Assert.Equal(Humanoid.HeadSocket, face.Socket.Id);
        Assert.Equal((.065f, .56f, .58f, "relaxed"), (face.Instance.X, face.Instance.FitX, face.Instance.FitY, face.Instance.Expression));
        Assert.Equal("head", person.Parameters.BindingOfAttachment(PersonTemplate.FaceInstance, RigProperties.FitX)!.Value.Parameter.Id);
        Assert.Equal("head", person.Parameters.BindingOfAttachment(PersonTemplate.FaceInstance, RigProperties.FitY)!.Value.Parameter.Id);
        Assert.Equal(CharacterModel.CurrentVersion, person.Base.Version);
        // The baked asset is the generator's output, byte for byte; the file is the retained generator's only product.
        Assert.Equal(ProceduralFace.Bake().ToJson(), File.ReadAllText(Catalog.PathOf(ProceduralFace.AssetId)));
        Assert.Equal(ProceduralFace.Names.Count, face.Asset.Expressions.Count);
        Assert.All(ProceduralFace.Names, word => Assert.True(face.Asset.Binds(word)));
        // No face field survives in any shipped file: the model, the 28 converted clips (now at version 4, plays on the instance) and the variants.
        var modelJson = File.ReadAllText(Catalog.PathOf("person"));
        Assert.DoesNotContain("faceX", modelJson);
        var played = Catalog.Animations.Values.Where(c => c.Model == "person" && c.Plays.Count > 0).ToList();
        Assert.Equal(28, played.Count);
        Assert.All(played, c => Assert.Equal([PersonTemplate.FaceInstance], c.Plays.Select(p => p.Instance)));
        Assert.All(played, c => Assert.Equal(MotionClip.CurrentVersion, c.Version));
        Assert.DoesNotContain(Directory.EnumerateFiles(Path.Combine(TestModels.AuthoredRoot, "animations")), f => File.ReadAllText(f).Contains("\"faces\""));
        foreach (var (id, word) in new[] { ("brute", "angry"), ("cinder", "smug"), ("club-caveman-build", "smug"), ("rock-thrower-build", "smug"), ("shield-defender-build", "smug") })
        {
            var variant = Catalog.Variants[id];
            Assert.Equal(ModelVariant.CurrentVersion, variant.Version);
            Assert.Equal(word, variant.Override(PropertyRef.OfAttachment(PersonTemplate.FaceInstance, RigProperties.Expression))!.Selection);
            Assert.DoesNotContain(variant.Overrides, o => o.Target.Property is "face" or "faceX");
            Assert.Equal(word, PoseEvaluator.Rest(Catalog.Resolve(id)).Expressions[PersonTemplate.FaceInstance]);
        }
        Assert.Equal(["slate", "ember", "night"], person.Base.Looks.Where(l => l.Expressions.Count > 0).Select(l => l.Id));
        Assert.Equal("determined", person.Base.Looks.Single(l => l.Id == "ember").Expressions[PersonTemplate.FaceInstance]);
        Assert.Empty(LegacySourceFields.Remaining(TestModels.AuthoredRoot).Where(f => f.Kind == "variant"));
        Assert.Equal(11, Catalog.Entities.Count);
    }

    [Fact]
    public void ClipsSelectExpressionsAndTheBackViewHidesTheFaceThroughTheOrdinaryChannel()
    {
        var hero = Catalog.Entities["hero"];
        var hit = Catalog.Animations["player-hit"];
        Assert.Equal("hurt", PoseEvaluator.Sample(hero.Model, hit, .1).Expressions[PersonTemplate.FaceInstance]);
        Assert.Equal("strained", PoseEvaluator.Sample(hero.Model, hit, .3).Expressions[PersonTemplate.FaceInstance]);
        var climb = Catalog.Animations["player-climb"];
        var back = PoseEvaluator.Sample(hero.Model, climb, .4, repeat: true);
        Assert.Equal(PartAsset.None, back.Expressions[PersonTemplate.FaceInstance]);
        Assert.True(back.Attachments[PersonTemplate.FaceInstance].Hidden);
        // The climb-on clip hides the face from its 'none' key on: shown just before, hidden just after.
        var on = Catalog.Animations["player-climb-on"];
        var hiddenFrom = on.Plays.Single().Keys.First(k => k.Expression == PartAsset.None).Time;
        Assert.True(hiddenFrom > 0);
        Assert.False(PoseEvaluator.Sample(hero.Model, on, hiddenFrom - .01f).Attachments[PersonTemplate.FaceInstance].Hidden);
        Assert.True(PoseEvaluator.Sample(hero.Model, on, hiddenFrom + .01f).Attachments[PersonTemplate.FaceInstance].Hidden);
        // A masked action (the gun shot over the walk) owns the face only when its group names the instance; the walk has no key, so the instance default shows beneath.
        var walk = Catalog.Animations["person-walk"];
        var shot = hero.Actions[EntityControllers.Shoot];
        var mask = hero.Model.Base.Groups.Single(g => g.Id == "upper").Targets.ToHashSet(StringComparer.Ordinal);
        var layered = PoseEvaluator.Sample(hero.Model, walk, .3, repeat: true, new() { Overlay = new(shot.Clip, .05, mask, 1) });
        Assert.Equal("relaxed", layered.Expressions[PersonTemplate.FaceInstance]);
        var owning = PoseEvaluator.Sample(hero.Model, walk, .3, repeat: true, new() { Overlay = new(shot.Clip, .05, mask.Append(PersonTemplate.FaceInstance).ToHashSet(StringComparer.Ordinal), 1) });
        Assert.Equal(shot.Clip.PlayAt(PersonTemplate.FaceInstance, .05f)!.Value.Expression, owning.Expressions[PersonTemplate.FaceInstance]);
    }

    [Fact]
    public void GameplayAsksForASemanticWordAndTheAssetMapsItToAPoseBlinkAndVisibility()
    {
        var person = Catalog.Resolve("person");
        var face = person.Attachment(PersonTemplate.FaceInstance)!;
        EvaluatedAttachment At(string? word, double clock) => PoseEvaluator.Rest(person, new(word) { Clock = clock }).Attachments[PersonTemplate.FaceInstance];
        // Relaxed: pupils shown at the relaxed opening, lids and cross hidden, mouth a curve; the blink layer closes the eyes at the old 3.62 s instant on the actor's clock.
        var relaxed = At(null, 1);
        Assert.Equal("relaxed", relaxed.Expression);
        Assert.Contains(relaxed.DrawnParts(), p => p.Id == "left-pupil");
        Assert.DoesNotContain(relaxed.DrawnParts(), p => p.Id is "left-lid" or "left-cross" or "mouth-open" or "left-white");
        Assert.Equal(.8f, relaxed.Pose.Fits["left-pupil"].Y, 4);
        Assert.Equal(0, At(null, 3.62).Pose.Fits["left-pupil"].Y, 4);
        Assert.Equal(.8f, At(null, 3.7).Pose.Fits["left-pupil"].Y, 4);
        // Hurt: eyes nearly shut, so the lid curves draw instead of the pupils; the mouth opens; the blink is muted.
        var hurt = At("hurt", 3.62);
        Assert.Equal("hurt", hurt.Expression);
        Assert.Contains(hurt.DrawnParts(), p => p.Id == "left-lid");
        Assert.DoesNotContain(hurt.DrawnParts(), p => p.Id == "left-pupil");
        Assert.Contains(hurt.DrawnParts(), p => p.Id == "mouth-open");
        // Knocked out: crossed eyes, the blink muted; surprised: whites and dots behind wide pupils.
        var knockedOut = At("knocked-out", 3.62);
        Assert.Contains(knockedOut.DrawnParts(), p => p.Id == "left-cross");
        Assert.DoesNotContain(knockedOut.DrawnParts(), p => p.Id is "left-pupil" or "left-lid");
        var surprised = At("surprised", 1);
        Assert.Contains(surprised.DrawnParts(), p => p.Id is "left-white");
        Assert.Contains(surprised.DrawnParts(), p => p.Id is "left-dot");
        Assert.True(surprised.Pose.Fits["left-pupil"].X > 1.5f);
        // The features sit over the head as before: the pupils straddle the head centre, offset by the old faceX, inside the head ellipse.
        var pose = PoseEvaluator.Rest(person);
        var head = pose.World(Humanoid.Head);
        var left = pose.Attachments[PersonTemplate.FaceInstance].World("left-eye");
        var right = pose.Attachments[PersonTemplate.FaceInstance].World("right-eye");
        Assert.Equal(head.X + .065f, (left.X + right.X) / 2, 3);
        Assert.Equal(.205f * .56f * 2, right.X - left.X, 3);
        Assert.True(left.Y > head.Y && left.Y < head.Y + .29f);
        // A word the asset does not bind leaves the clip or default in force instead of failing gameplay.
        Assert.Equal("relaxed", At("bewildered", 1).Expression);
        // A wider head carries the face with it: the same parameter scales the instance fit (stretch policy, as the old features scaled with the head bounds).
        var wide = ResolvedModel.From(person.Base, new ModelVariant { Id = "wide", Name = "Wide", Base = "person", Parameters = { ["head"] = 1.4f } }, Catalog.Parts.GetValueOrDefault);
        var widePose = PoseEvaluator.Rest(wide);
        var wideSpan = widePose.Attachments[PersonTemplate.FaceInstance].World("right-eye").X - widePose.Attachments[PersonTemplate.FaceInstance].World("left-eye").X;
        Assert.Equal(1.4f, wideSpan / (right.X - left.X), 3);
        Assert.Equal(PropertySource.ParameterBinding, wide.Provenance(PropertyRef.OfAttachment(PersonTemplate.FaceInstance, RigProperties.FitX)));
        // Seeking is pure and the face clips dispatch nothing: no markers, same pose twice.
        Assert.All(face.Asset.Clips, c => Assert.Empty(c.Markers));
        var a = At("hurt", 2).Pose.Points["mouth-bend"];
        var b = At("hurt", 2).Pose.Points["mouth-bend"];
        Assert.Equal(a, b);
    }

    [Fact]
    public void TheWalkingHeroBlinksWhileSmilingAndSwapsItsFaceAsset()
    {
        var hero = Catalog.Entities["hero"];
        var walk = Catalog.Animations["person-walk"];
        var smiling = PoseEvaluator.Sample(hero.Model, walk, .3, repeat: true, new("happy") { Clock = 3.62 });
        Assert.Equal("happy", smiling.Expressions[PersonTemplate.FaceInstance]);
        Assert.NotEqual(PoseEvaluator.Rest(hero.Model).Points[Humanoid.LeftFoot], smiling.Points[Humanoid.LeftFoot]);
        // Happy mutes the blink (only relaxed, focused and worried blink, as the old presentation did); relaxed blinks at the same instant.
        Assert.True(smiling.Attachments[PersonTemplate.FaceInstance].Pose.Fits.GetValueOrDefault("left-pupil", Vector2.One).Y > .5f);
        Assert.Equal(0, PoseEvaluator.Sample(hero.Model, walk, .3, repeat: true, new("relaxed") { Clock = 3.62 }).Attachments[PersonTemplate.FaceInstance].Pose.Fits["left-pupil"].Y, 4);
        // The same instance wears the curve face instead: its default word must be cleared first (the curve face binds other words), then the swap holds.
        var person = CharacterModel.FromJson(hero.Model.Base.ToJson());
        Assert.Contains("binds no expression 'relaxed'", Assert.Throws<InvalidDataException>(() =>
        {
            ModelAuthoring.SetAttachmentAsset(person, PersonTemplate.FaceInstance, "face-curve", Catalog.Parts.GetValueOrDefault);
            ResolvedModel.From(person, null, Catalog.Parts.GetValueOrDefault);
        }).Message);
        person.Attachments[0].Expression = null;
        var curved = ResolvedModel.From(person, null, Catalog.Parts.GetValueOrDefault);
        Assert.Equal("face-curve", curved.Attachment(PersonTemplate.FaceInstance)!.Asset.Id);
        Assert.Equal("neutral", PoseEvaluator.Rest(curved).Expressions[PersonTemplate.FaceInstance]);
        Assert.Equal("smile", PoseEvaluator.Sample(curved, walk, .3, repeat: true, new("smile")).Expressions[PersonTemplate.FaceInstance]);
        Assert.Equal("relaxed", PoseEvaluator.Rest(hero.Model).Expressions[PersonTemplate.FaceInstance]);
    }

    [Fact]
    public void TheConversionRunsOnAScratchTreeWithAReportRerunsUnchangedAndRefusesTheLegacyFieldsElsewhere()
    {
        // A small legacy bundle in the retired form: a version-10 model with the face payload on its head and a look word,
        // a version-3 clip with a face track, a version-4 variant with a face word and a version-6 variant with the typed face override.
        var authored = Path.Combine(_root, "authored");
        Directory.CreateDirectory(Path.Combine(authored, "models"));
        Directory.CreateDirectory(Path.Combine(authored, "animations"));
        Directory.CreateDirectory(Path.Combine(authored, "variants"));
        Directory.CreateDirectory(Path.Combine(authored, "parts"));
        File.WriteAllText(Path.Combine(authored, "parts", ProceduralFace.AssetId + ".json"), PersonTemplate.Face.ToJson());
        var model = JsonNode.Parse(PersonTemplate.Model().ToJson())!.AsObject();
        model["version"] = 10;
        var head = model["parts"]!.AsArray().OfType<JsonObject>().First(p => p["id"]!.ToString() == Humanoid.Head);
        head["face"] = new JsonObject { ["expression"] = "focused", ["offsetX"] = .065f };
        model["looks"]!.AsArray().OfType<JsonObject>().First(l => l["id"]!.ToString() == "ember")["parts"]![Humanoid.Head] = new JsonObject { ["face"] = "determined" };
        File.WriteAllText(Path.Combine(authored, "models", "person.json"), model.ToJsonString());
        var clip = JsonNode.Parse(ClipAuthoring.New(ResolvedModel.From(PersonTemplate.Model()), "wince", "Wince", loop: false).ToJson())!.AsObject();
        clip["version"] = 3;
        clip["faces"] = new JsonArray(new JsonObject { ["part"] = Humanoid.Head, ["keys"] = new JsonArray(new JsonObject { ["time"] = 0, ["expression"] = "hurt" }, new JsonObject { ["time"] = .5f, ["expression"] = "none" }) });
        File.WriteAllText(Path.Combine(authored, "animations", "wince.json"), clip.ToJsonString());
        File.WriteAllText(Path.Combine(authored, "variants", "old.json"), """{"format":"app2d-variant","version":4,"id":"old","name":"Old","base":"person","build":{"legs":1.1},"rest":{},"parts":{"head":{"face":"smug"}}}""");
        var typed = new ModelVariant { Id = "typed", Name = "Typed", Base = "person", Overrides = [new() { Target = PropertyRef.OfPart(Humanoid.Head, "face"), Selection = "angry" }] };
        File.WriteAllText(Path.Combine(authored, "variants", "typed.json"), typed.ToJson());
        // The retired fields are refused by name everywhere but the conversion.
        Assert.Contains("FaceConversion.ConvertPayload", Assert.Throws<InvalidDataException>(() => CharacterModel.FromJson(File.ReadAllText(Path.Combine(authored, "models", "person.json")))).Message);
        Assert.Contains("retired in P7c", Assert.Throws<InvalidDataException>(() => MotionClip.FromJson(File.ReadAllText(Path.Combine(authored, "animations", "wince.json")))).Message);
        Assert.Contains("convert the bundle", Assert.Throws<InvalidDataException>(() => ModelVariant.FromJson(File.ReadAllText(Path.Combine(authored, "variants", "old.json")))).Message);
        var output = Path.Combine(_root, "scratch");
        var result = FaceConversion.ConvertPayload(authored, "person", Humanoid.Head, PersonTemplate.FaceInstance, ProceduralFace.AssetId, Humanoid.HeadSocket, output);
        Assert.True(result.Converted, string.Join("; ", result.Differences));
        Assert.True(File.Exists(result.ReportPath));
        Assert.Equal(["model", "clip", "variant", "variant"], result.Files.Select(f => f.Kind));
        Assert.All(result.Files, f => Assert.StartsWith("converted", f.Status));
        // The outputs: the model wears the face with the payload's offset and the look's word, the clip selects the words, both variants override the expression; the source is untouched.
        var converted = CharacterModel.Load(Path.Combine(output, "models", "person.json"));
        Assert.Equal(CharacterModel.CurrentVersion, converted.Version);
        Assert.Equal((.065f, "focused"), (converted.Attachments.Single().X, converted.Attachments.Single().Expression));
        Assert.Equal("determined", converted.Looks.Single(l => l.Id == "ember").Expressions[PersonTemplate.FaceInstance]);
        var wince = MotionClip.FromJson(File.ReadAllText(Path.Combine(output, "animations", "wince.json")));
        Assert.Equal(MotionClip.CurrentVersion, wince.Version);
        Assert.Equal([("hurt", 0f), (PartAsset.None, .5f)], wince.Plays.Single().Keys.Select(k => (k.Expression, k.Time)));
        Assert.Equal("smug", ModelVariant.FromJson(File.ReadAllText(Path.Combine(output, "variants", "old.json"))).Override(PropertyRef.OfAttachment(PersonTemplate.FaceInstance, RigProperties.Expression))!.Selection);
        Assert.Equal("angry", ModelVariant.FromJson(File.ReadAllText(Path.Combine(output, "variants", "typed.json"))).Override(PropertyRef.OfAttachment(PersonTemplate.FaceInstance, RigProperties.Expression))!.Selection);
        Assert.Contains("\"version\":10", File.ReadAllText(Path.Combine(authored, "models", "person.json")).Replace(" ", ""));
        var again = FaceConversion.ConvertPayload(authored, "person", Humanoid.Head, PersonTemplate.FaceInstance, ProceduralFace.AssetId, Humanoid.HeadSocket, output);
        Assert.All(again.Files, f => Assert.StartsWith("unchanged", f.Status));
        var edited = CharacterModel.Load(Path.Combine(output, "models", "person.json"));
        edited.Name = "Edited";
        edited.Save(Path.Combine(output, "models", "person.json"));
        var kept = FaceConversion.ConvertPayload(authored, "person", Humanoid.Head, PersonTemplate.FaceInstance, ProceduralFace.AssetId, Humanoid.HeadSocket, output);
        Assert.StartsWith("kept", kept.Files.First(f => f.Kind == "model").Status);
        // A version-3 clip without face tracks reads unchanged at version 4.
        var plain = JsonNode.Parse(ClipAuthoring.New(ResolvedModel.From(PersonTemplate.Model()), "plain", "Plain").ToJson())!.AsObject();
        plain["version"] = 3;
        Assert.Equal(MotionClip.CurrentVersion, MotionClip.FromJson(plain.ToJsonString()).Version);
    }
}
