using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using App2d.Core.Characters.Editing;
using System.Text.Json;

namespace App2d.Tests.Authored;

public sealed class ModelTemplateTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "model-templates-" + Guid.NewGuid().ToString("N"));
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Theory]
    [InlineData("person", 7)]
    [InlineData("quadruped", 8)]
    [InlineData("triceratops", 8)]
    [InlineData("stalker", 5)]
    public void ShippedTemplatesPreserveSourcePosesAndRemapEveryMotionSet(string templateId, int clipCount)
    {
        var assets = AuthoringWorkspace.Open(TestModels.AuthoredRoot);
        Assert.Empty(assets.LoadErrors);
        var sourceJson = assets.Documents.ToDictionary(d => d.Id, d => d.Serialize());
        var recipe = assets.ModelTemplates[templateId];
        var source = assets.Resolve(recipe.Model)!;
        var session = new EditorSession(assets);
        Assert.True(session.NewModel("independent", "Independent", templateId), session.Message);
        var copy = assets.Resolve("independent")!;
        var clips = assets.Clips.Where(c => c.Asset.Model == "independent").ToArray();
        Assert.Equal(clipCount, clips.Length);
        Assert.All(clips, c => Assert.True(c.IsNew));
        foreach (var (suffix, sourceId) in recipe.Animations)
        {
            var original = assets.Clip(sourceId)!.Asset;
            var created = assets.Clip("independent-" + suffix)!.Asset;
            foreach (var phase in new[] { 0, .17f, .5f, .83f, 1 })
            {
                var before = PoseEvaluator.Sample(source, original, original.Duration * phase);
                var after = PoseEvaluator.Sample(copy, created, created.Duration * phase);
                foreach (var point in before.Points) TestModels.Near(point.Value, after.Points[point.Key], 1e-6f);
                Assert.Equal(before.Angles.OrderBy(p => p.Key), after.Angles.OrderBy(p => p.Key));
                Assert.Equal(before.Contacts, after.Contacts);
            }
        }
        var copiedIds = clips.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        Assert.All(copy.Base.MotionSets.SelectMany(s => s.Roles.Values), id => Assert.Contains(id, copiedIds));
        foreach (var (id, json) in sourceJson)
        {
            Assert.Equal(json, assets.Find(id)!.Serialize());
            Assert.False(assets.Find(id)!.Dirty);
        }
    }

    [Fact]
    public void AJsonRecipeCreatesAnUnrelatedBoneRigAndSavesAnIndependentBundle()
    {
        SeedMachine();
        var assets = AuthoringWorkspace.Open(_root); var session = new EditorSession(assets);
        Assert.Empty(assets.LoadErrors);
        Assert.Contains("oscillator", assets.ModelTemplates.Keys);
        Assert.True(session.NewModel("crank", "Crank", "oscillator"), session.Message);
        Assert.Equal("crank-swing", session.ClipId); // The default clip does not have to be called idle.
        var model = assets.Model("crank")!; var clip = assets.Clip("crank-swing")!;
        Assert.Equal("crank-swing", model.Asset.MotionSets.Single().Roles["operate"]);
        Assert.Equal("source-performance", clip.Asset.Markers.Single().Id); // Opaque labels are not text-replaced.
        Assert.True(session.Edit(clip, () => clip.Asset.Tracks[0].Keys[1].Angle = .8f), session.Message);
        Assert.Equal(.4f, assets.Clip("source-performance")!.Asset.Tracks[0].Keys[1].Angle);
        session.SaveAll(); Assert.False(session.MessageIsError, session.Message);
        var reopened = AuthoredCatalog.Load(_root);
        Assert.Empty(reopened.Errors);
        var pose = PoseEvaluator.Sample(reopened.Resolve("crank"), reopened.Animations["crank-swing"], 1);
        Assert.Equal(.8f, pose.Angles["pendulum"], 5);
        Assert.Equal(.4f, reopened.Animations["source-performance"].Tracks[0].Keys[1].Angle);
    }

    [Theory]
    [InlineData("missing-model")]
    [InlineData("missing-clip")]
    [InlineData("unlisted-role")]
    [InlineData("incompatible-clip")]
    [InlineData("model-collision")]
    [InlineData("clip-collision")]
    [InlineData("unreadable-collision")]
    [InlineData("long-id")]
    public void InvalidOrConflictingBundlesNeverLeavePartialDocuments(string failure)
    {
        SeedMachine();
        var assets = AuthoringWorkspace.Open(_root); var session = new EditorSession(assets);
        var recipe = assets.ModelTemplates["oscillator"];
        var id = "copy";
        switch (failure)
        {
            case "missing-model": recipe.Model = "missing"; break;
            case "missing-clip": recipe.Animations["swing"] = "missing"; break;
            case "unlisted-role": assets.Model("machine")!.Asset.MotionSets[0].Roles["operate"] = "missing"; break;
            case "incompatible-clip": assets.Clip("source-performance")!.Asset.Model = "other"; break;
            case "model-collision": id = "machine"; break;
            case "clip-collision":
                var second = MotionClip.FromJson(assets.Clip("source-performance")!.Serialize());
                second.Id = "copy-second"; assets.Create(second);
                recipe.Animations["second"] = second.Id; // The first copy could succeed; the second must prevent the entire bundle.
                break;
            case "unreadable-collision": File.WriteAllText(Path.Combine(_root, "animations", "copy-swing.json"), "{ broken"); break;
            case "long-id": id = new string('a', 64); break; // Valid model ID; invalid generated clip ID.
        }
        var before = assets.Documents.ToDictionary(d => d.Id, d => d.Serialize());
        var subject = session.SubjectId;
        Assert.False(session.NewModel(id, "Copy", "oscillator"));
        Assert.True(session.MessageIsError);
        Assert.Equal(subject, session.SubjectId);
        Assert.Equal(before.Keys.Order(), assets.Documents.Select(d => d.Id).Order());
        foreach (var (key, json) in before) Assert.Equal(json, assets.Find(key)!.Serialize());
        if (failure == "unreadable-collision") Assert.Equal("{ broken", File.ReadAllText(Path.Combine(_root, "animations", "copy-swing.json")));
    }

    [Fact]
    public void BadTemplateFilesAreReportedWithoutBlockingOtherRecipesOrBlankModels()
    {
        SeedMachine();
        File.WriteAllText(Path.Combine(_root, "templates", "broken.json"), "{ broken");
        File.Copy(Path.Combine(_root, "templates", "oscillator.json"), Path.Combine(_root, "templates", "duplicate.json"));
        var assets = AuthoringWorkspace.Open(_root);
        Assert.Equal(2, assets.LoadErrors.Count);
        Assert.Contains(assets.LoadErrors, e => e.Contains("broken.json"));
        Assert.Contains(assets.LoadErrors, e => e.Contains("Duplicate template id"));
        Assert.Single(assets.ModelTemplates);
        var session = new EditorSession(assets);
        Assert.True(session.NewModel("blank", "Blank", "empty"), session.Message);
        Assert.True(session.EditRig);
        Assert.Empty(session.SubjectModel!.Asset.Controls);
    }

    [Theory]
    [InlineData("\"version\": 2")]
    [InlineData("\"animations\": null")]
    [InlineData("\"animations\": {\"a\": \"clip\", \"b\": \"clip\"}")]
    [InlineData("\"previewAnimation\": \"missing\"")]
    [InlineData("\"animatons\": {}")]
    public void RecipesRejectUnsupportedVersionsTyposAndAmbiguousReferences(string extra)
    {
        var json = "{\"id\":\"test\",\"name\":\"Test\",\"model\":\"machine\"," + extra + "}";
        var error = Record.Exception(() => ModelTemplate.FromJson(json));
        Assert.True(error is InvalidDataException or JsonException, error?.ToString() ?? "The invalid recipe was accepted.");
    }

    private void SeedMachine()
    {
        var model = ModelAuthoring.Empty("machine", "Machine");
        var root = ModelAuthoring.AddBone(model, null, "mount");
        var child = ModelAuthoring.AddBone(model, root.Id, "pendulum");
        ModelAuthoring.AddPartToBone(model, PuppetPartKinds.Box, child.Id);
        model.MotionSets = [new() { Id = "cycle", Name = "Cycle", Roles = new() { ["operate"] = "source-performance" } }];
        var clip = ClipAuthoring.New(ResolvedModel.From(model), "source-performance", "Swing", 1, true);
        clip.Tracks = [new() { Kind = MotionClip.RotateKind, Target = child.Id, Keys = [new(), new() { Time = 1, Angle = .4f }] }];
        clip.Markers = [new() { Id = "source-performance", Time = .5f }];
        model.Save(Path.Combine(_root, "models", "machine.json"));
        clip.Save(Path.Combine(_root, "animations", "source-performance.json"));
        var template = new ModelTemplate
        {
            Id = "oscillator",
            Name = "Oscillator",
            Model = model.Id,
            Animations = new() { ["swing"] = clip.Id },
            PreviewAnimation = "swing"
        };
        AuthoredAsset.Write(Path.Combine(_root, "templates", "oscillator.json"), JsonSerializer.Serialize(template, AuthoredJson.Options));
    }
}
