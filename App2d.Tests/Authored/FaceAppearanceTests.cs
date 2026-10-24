using App2d.CharacterSeeds;
using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Drawing;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Curves;
using App2d.Core.Shapes;
using System.Numerics;
using System.Text.Json.Nodes;

namespace App2d.Tests.Authored;

/// <summary>P2b of the character-system migration: the procedural face is an optional payload of the part that renders it.</summary>
public sealed class FaceAppearanceTests
{
    private static CharacterModel Person => PersonTemplate.Model();
    private static PuppetPart Head(CharacterModel model) => model.Parts.Single(p => p.Id == Humanoid.Head);

    [Fact]
    public void OnlyTheHeadCarriesAFaceAndTheSharedPartTypeHasNoFaceFields()
    {
        var person = Person;
        Assert.Equal([Humanoid.Head], person.Parts.Where(p => p.Face is not null).Select(p => p.Id));
        Assert.Equal(new FaceAppearance { Expression = "relaxed", OffsetX = .065f }, Head(person).Face);
        Assert.DoesNotContain(typeof(PuppetPart).GetProperties(), p => p.Name is "FaceX" or "WaveAmplitude" or "WaveCycles" or "WavePhaseControl");
        var json = person.ToJson();
        Assert.Contains("\"face\": {", json);
        Assert.DoesNotContain("faceX", json);
        Assert.Equal(json, CharacterModel.FromJson(json).ToJson());
    }

    /// <summary>The compatibility boundary (LegacyModelReader): the loose expression string and faceX become the payload; the file is not touched.</summary>
    [Fact]
    public void VersionNineFaceFieldsReadIntoThePayload()
    {
        var node = JsonNode.Parse(Person.ToJson())!.AsObject();
        node["version"] = 9;
        var parts = node["parts"]!.AsArray();
        var head = parts.Single(p => p!["id"]!.GetValue<string>() == Humanoid.Head)!.AsObject();
        var body = parts.Single(p => p!["id"]!.GetValue<string>() == Humanoid.Body)!.AsObject();
        head["face"] = "determined";
        head["faceX"] = .05f;
        body["face"] = "none";
        body["faceX"] = .2f;
        var read = CharacterModel.FromJson(node.ToJsonString());
        Assert.Equal(CharacterModel.CurrentVersion, read.Version);
        Assert.Equal(new FaceAppearance { Expression = "determined", OffsetX = .05f }, Head(read).Face);
        Assert.Null(read.Parts.Single(p => p.Id == Humanoid.Body).Face);
        Assert.Equal([Humanoid.Head], read.Parts.Where(p => p.Face is not null).Select(p => p.Id));
        var written = read.ToJson();
        Assert.Contains($"\"version\": {CharacterModel.CurrentVersion}", written);
        Assert.DoesNotContain("faceX", written);
        // Every part's face is the payload object or absent; a look's face stays the override word it always was.
        Assert.All(JsonNode.Parse(written)!["parts"]!.AsArray(), p => Assert.True(p!["face"] is null or JsonObject));
        Assert.Equal(written, CharacterModel.FromJson(written).ToJson());
        var nulled = JsonNode.Parse(node.ToJsonString())!.AsObject();
        nulled["parts"]!.AsArray().Single(p => p!["id"]!.GetValue<string>() == Humanoid.Head)!["face"] = null;
        Assert.Contains("face is null", Assert.Throws<InvalidDataException>(() => CharacterModel.FromJson(nulled.ToJsonString())).Message);

        // The shipped Person file still carries the loose fields at version 8 and reads through the boundary unchanged.
        var file = File.ReadAllText(Path.Combine(TestModels.AuthoredRoot, "models", "person.json"));
        Assert.Contains("\"version\": 8", file);
        Assert.Contains("\"face\": \"relaxed\"", file);
        Assert.Contains("\"faceX\": 0.065", file);
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        Assert.Empty(catalog.Errors);
        Assert.Equal([("person", Humanoid.Head)], catalog.Models.Values.SelectMany(m => m.Parts.Where(p => p.Face is not null).Select(p => (m.Id, p.Id))));
        Assert.Equal(new FaceAppearance { Expression = "relaxed", OffsetX = .065f }, Head(catalog.Models["person"]).Face);
        foreach (var clip in catalog.Animations.Values.Where(c => c.Faces.Count > 0))
        {
            Assert.All(clip.Faces, f => Assert.Equal(Humanoid.Head, f.Part));
        }
    }

    [Theory]
    [InlineData("curve")]
    [InlineData("expression")]
    [InlineData("offset")]
    public void TheFaceValidatesItsOwnShapeVocabularyAndOffset(string failure)
    {
        var person = Person;
        var head = Head(person);
        switch (failure)
        {
            case "curve":
                head.B = Humanoid.Chest;
                head.Geometry = new LineSegmentCurve2D { Start = Vector2.Zero, End = Vector2.UnitY }.Validated();
                head.Material = new();
                break;
            case "expression":
                head.Face = head.Face! with { Expression = "grinning" };
                break;
            case "offset":
                head.Face = head.Face! with { OffsetX = 3 };
                break;
        }
        var message = Assert.Throws<InvalidDataException>(person.Validate).Message;
        Assert.Contains(failure switch { "curve" => "shape part", "expression" => "grinning", _ => "offsetX" }, message);
    }

    [Fact]
    public void FacePropertiesTracksAndBindingsNeedThePayload()
    {
        var person = Person;
        string Refusal(PropertyOverride change)
        {
            var odd = new ModelVariant { Id = "odd", Name = "Odd", Base = person.Id, Overrides = [change] };
            return Assert.Throws<InvalidDataException>(() => ResolvedModel.From(person, odd)).Message;
        }
        Assert.Contains("has no face", Refusal(PropertyOverride.Of(PropertyRef.OfPart(Humanoid.Body, RigProperties.Face), "angry")));
        Assert.Contains("has no face", Refusal(PropertyOverride.Of(PropertyRef.OfPart(Humanoid.Body, RigProperties.FaceX), .1f)));
        Assert.DoesNotContain(RigProperties.Declarations(person.Parts.Single(p => p.Id == Humanoid.Body)), d => RigProperties.FaceProperties.Contains(d.Name));
        Assert.Contains(RigProperties.Declarations(Head(person)), d => d.Name == RigProperties.Face);

        person.Parameters[0].Bindings.Add(new() { Target = PropertyRef.OfPart(Humanoid.Body, RigProperties.FaceX) });
        Assert.Contains("has no face", Assert.Throws<InvalidDataException>(person.Validate).Message);
        person.Parameters[0].Bindings.RemoveAt(person.Parameters[0].Bindings.Count - 1);

        var model = ResolvedModel.From(person);
        var clip = new MotionClip { Id = "grin", Name = "Grin", Model = "person", Duration = 1, Faces = [new() { Part = Humanoid.Body, Keys = [new() { Expression = "happy" }] }] };
        Assert.Contains("no part with a face", Assert.Throws<InvalidDataException>(() => clip.Validate(model)).Message);
        clip.Faces[0].Part = Humanoid.Head;
        clip.Validate(model);
        Assert.Equal("happy", PoseEvaluator.Sample(model, clip, .5).Expressions[Humanoid.Head]);
    }

    [Fact]
    public void AVariantRestylesOrHidesTheFaceAndCannotAddOrRemoveIt()
    {
        var person = Person;
        var variant = new ModelVariant { Id = "stern", Name = "Stern", Base = person.Id };
        variant.Set(PropertyOverride.Of(PropertyRef.OfPart(Humanoid.Head, RigProperties.Face), "angry"));
        variant.Set(PropertyOverride.Of(PropertyRef.OfPart(Humanoid.Head, RigProperties.FaceX), .1f));
        var resolved = ResolvedModel.From(person, variant);
        Assert.Equal(new FaceAppearance { Expression = "angry", OffsetX = .1f }, Head(resolved.Base).Face is null ? null : resolved.Parts.Single(p => p.Id == Humanoid.Head).Face);
        Assert.Equal(new FaceAppearance { Expression = "relaxed", OffsetX = .065f }, Head(person).Face); // the base is untouched
        Assert.Equal(PropertySource.Override, resolved.Provenance(PropertyRef.OfPart(Humanoid.Head, RigProperties.Face)));

        // Clearing the expression hides the face: the payload stays, the pose carries "none" and the drawing adds no features.
        variant.Set(PropertyOverride.Cleared(PropertyRef.OfPart(Humanoid.Head, RigProperties.Face)));
        var hidden = ResolvedModel.From(person, variant);
        Assert.Equal(FaceAppearance.None, hidden.Parts.Single(p => p.Id == Humanoid.Head).Face!.Expression);
        Assert.Equal(FaceAppearance.None, PoseEvaluator.Rest(hidden).Expressions[Humanoid.Head]);
        var drawing = new PuppetDrawing();
        drawing.Build(hidden, PoseEvaluator.Rest(hidden));
        var withoutFace = drawing.Mesh.Count;
        drawing.Build(resolved, PoseEvaluator.Rest(resolved));
        Assert.True(drawing.Mesh.Count > withoutFace);

        // The inspector's part edit route turns expression and offset edits into overrides and refuses adding or removing the payload.
        var edited = new ModelVariant { Id = "edited", Name = "Edited", Base = person.Id };
        ModelAuthoring.EditPartOverride(edited, person, Head(ResolvedModel.From(person).Base), p => p.Face = p.Face! with { Expression = "smug" });
        Assert.Equal("smug", edited.Override(PropertyRef.OfPart(Humanoid.Head, RigProperties.Face))!.Selection);
        Assert.Null(edited.Override(PropertyRef.OfPart(Humanoid.Head, RigProperties.FaceX)));
        Assert.Contains("owns the payload", Assert.Throws<InvalidOperationException>(() => ModelAuthoring.EditPartOverride(edited, person, Head(person), p => p.Face = null)).Message);
    }

    [Fact]
    public void TheFaceDrawsTheSameFeaturesOffsetAlongThePartFrame()
    {
        var person = Person;
        var head = Head(person);
        var model = ResolvedModel.From(person);
        var pose = PoseEvaluator.Rest(model);
        Vector3[] Features(FaceAppearance? face)
        {
            var drawing = new PuppetDrawing();
            var part = head with { Face = face };
            drawing.Build(person.Ink, person.LineWidth, [part], pose.World, p => pose.Expressions.GetValueOrDefault(Humanoid.Head, "relaxed"), null, pose.Placement, id => model.Rest[id], model.RestPlacement);
            return [.. drawing.Mesh.Vertices.ToArray().Select(v => new Vector3(v.Position.X, v.Position.Y, v.Position.Z))];
        }
        var bare = Features(null);
        var centered = Features(new() { Expression = "relaxed" });
        var offset = Features(new() { Expression = "relaxed", OffsetX = .065f });
        Assert.True(centered.Length > bare.Length);
        Assert.Equal(centered.Length, offset.Length);
        // The shape itself is unchanged; every feature vertex moves by the offset along the frame's X axis (the head frame is upright at rest).
        for (var i = 0; i < bare.Length; i++)
        {
            Assert.Equal(bare[i], centered[i]);
        }

        for (var i = bare.Length; i < centered.Length; i++)
        {
            TestModels.Near(centered[i] + new Vector3(.065f, 0, 0), offset[i], 1e-5f);
            Assert.True(offset[i].Z < pose.World(Humanoid.Head).Z + head.Depth, "features sit in front of the head shape");
        }
        // A back view hides the features without touching the payload.
        var hidden = new PuppetDrawing();
        hidden.Build(person.Ink, person.LineWidth, [head], pose.World, _ => FaceAppearance.None, null, pose.Placement, id => model.Rest[id], model.RestPlacement);
        Assert.Equal(bare.Length, hidden.Mesh.Count);
    }

    [Fact]
    public void AddingAndRemovingTheFaceAreOrdinaryPartEditsAndACurveDropsIt()
    {
        var person = Person;
        var body = person.Parts.Single(p => p.Id == Humanoid.Body);
        Assert.True(FaceAppearance.Supports(body.Geometry));
        body.Face = FaceAppearance.Starter;
        person.Validate();
        Assert.Equal(2, person.Parts.Count(p => p.Face is not null));
        Assert.Equal("relaxed", PoseEvaluator.Rest(ResolvedModel.From(person)).Expressions[Humanoid.Body]);
        body.Face = null;
        person.Validate();
        Assert.Single(person.Parts, p => p.Face is not null);

        ModelAuthoring.SetPartGeometry(person, Humanoid.Head, new LineSegmentCurve2D { Start = Vector2.Zero, End = Vector2.UnitY }.Validated());
        Assert.Null(Head(person).Face);
        Assert.False(FaceAppearance.Supports(Head(person).Geometry));
        Assert.Empty(PoseEvaluator.Rest(ResolvedModel.From(person)).Expressions);
    }
}
