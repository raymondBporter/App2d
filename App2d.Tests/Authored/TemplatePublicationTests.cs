using App2d.CharacterStudio.Authoring;
using App2d.CharacterStudio.CharacterSeeds;
using App2d.CharacterStudio.Editing;
using App2d.Core.Characters.Authored;

namespace App2d.Tests.Authored;

/// <summary>P10a: anatomy templates are published resources, their contracts hold on the shipped models, and the publication is a rerunnable conversion with a report.</summary>
public sealed class TemplatePublicationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "template-publication-" + Guid.NewGuid().ToString("N"));
    private string Authored => Path.Combine(_root, "authored");
    private string Output => Path.Combine(_root, "scratch");

    public TemplatePublicationTests()
    {
        foreach (var file in Directory.EnumerateFiles(TestModels.AuthoredRoot, "*.json", SearchOption.AllDirectories))
        {
            var target = Path.Combine(Authored, Path.GetRelativePath(TestModels.AuthoredRoot, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }

    public void Dispose() => Directory.Delete(_root, true);

    [Fact]
    public void TheShippedTemplatesPublishTheirContractsAndTheyHoldOnTheShippedModels()
    {
        var assets = AuthoringWorkspace.Open(TestModels.AuthoredRoot);
        Assert.Empty(assets.LoadErrors);
        foreach (var id in new[] { "person", "quadruped", "triceratops", "pterosaur" })
        {
            var template = assets.ModelTemplates[id];
            Assert.Equal(ModelTemplate.CurrentVersion, template.Version);
            Assert.NotEmpty(template.Slots);
            Assert.Equal(3, template.Presets.Count);
            Assert.NotEmpty(template.Library.Roles);
            Assert.NotEmpty(template.Library.Masks);
            Assert.NotEmpty(template.Library.Mappings);
            Assert.Empty(assets.TemplateProblems(template));
            // Every dimension is a model parameter and every mapping names a measure of the model.
            var model = assets.Model(template.Model)!.Asset;
            Assert.All(template.Dimensions, d => Assert.Contains(model.Parameters, p => p.Id == d));
            Assert.All(template.Library.Mappings.Keys, m => Assert.Contains(model.Measures, x => x.Id == m));
        }
        Assert.Equal(ModelTemplate.CurrentVersion, assets.ModelTemplates["stalker"].Version);
        Assert.Empty(assets.TemplateProblems(assets.ModelTemplates["stalker"]));
        Assert.Equal(3, assets.ModelTemplates["person"].Presets.Count);
        Assert.Equal([PersonBuild.LegsId, PersonBuild.TorsoId, PersonBuild.ArmsId, PersonBuild.WidthId, PersonBuild.HeadId], assets.ModelTemplates["person"].Dimensions);
        var person = assets.ModelTemplates["person"].Slots.Single(s => s.Id == "head-art");
        Assert.Equal((TemplateResources.Face, true, ProceduralFace.AssetId), (person.Usage, person.Required, person.Default));
        // The quadruped, triceratops and flier models carry their dimensions as data now, at the current version.
        foreach (var id in new[] { "quadruped", "triceratops", "pterosaur" })
        {
            var model = assets.Model(id)!.Asset;
            Assert.Equal(CharacterModel.CurrentVersion, model.Version);
            Assert.Equal(3, model.Parameters.Count);
            Assert.Equal(3, model.Presets.Count);
            Assert.Contains($"\"version\": {CharacterModel.CurrentVersion}", File.ReadAllText(Path.Combine(TestModels.AuthoredRoot, "models", id + ".json")));
        }
    }

    [Fact]
    public void PublishingOnAScratchTreeReproducesTheShippedFilesRerunsUnchangedAndKeepsAnEditedOutput()
    {
        var results = TemplateResources.Publish(Authored, Output);
        Assert.All(results, r => Assert.True(r.Published, $"{r.TemplateId}: {r.Status} {string.Join("; ", r.Differences)}"));
        foreach (var result in results)
        {
            Assert.Equal(File.ReadAllText(Path.Combine(TestModels.AuthoredRoot, "models", result.ModelId + ".json")), File.ReadAllText(Path.Combine(Output, "models", result.ModelId + ".json")));
            Assert.Equal(File.ReadAllText(Path.Combine(TestModels.AuthoredRoot, "templates", result.TemplateId + ".json")), File.ReadAllText(Path.Combine(Output, "templates", result.TemplateId + ".json")));
            Assert.True(File.Exists(Path.Combine(Output, $"template-publication-{result.TemplateId}.md")));
            Assert.Contains(result.Report, line => line.Contains("clip(s) validate and sample identically"));
        }
        // The scratch tree is the published one, so the declaration re-declares the same dimensions and the presets resolve as shipped.
        // P11a: the legs dimension scales each leg bone's length (direction kept), so the long-legged preset reaches a little farther than when it lifted the joints' height alone (1.033).
        Assert.Contains(results.Single(r => r.TemplateId == "quadruped").Report, line => line.StartsWith("preset 'long-legged' resolves: leg 1.059", StringComparison.Ordinal));
        // The source tree is untouched.
        foreach (var file in Directory.EnumerateFiles(TestModels.AuthoredRoot, "*.json", SearchOption.AllDirectories))
        {
            Assert.Equal(File.ReadAllText(file), File.ReadAllText(Path.Combine(Authored, Path.GetRelativePath(TestModels.AuthoredRoot, file))));
        }
        var rerun = TemplateResources.Publish(Authored, Output);
        Assert.All(rerun, r => Assert.Equal("unchanged, template unchanged", r.Status));
        var edited = ModelTemplate.FromJson(File.ReadAllText(Path.Combine(Output, "templates", "pterosaur.json")));
        edited.Description = "Edited after publication.";
        edited.Save(Path.Combine(Output, "templates", "pterosaur.json"));
        var kept = TemplateResources.Publish(Authored, Output).Single(r => r.TemplateId == "pterosaur");
        Assert.Equal("unchanged, template kept", kept.Status);
        Assert.Equal("Edited after publication.", ModelTemplate.FromJson(File.ReadAllText(Path.Combine(Output, "templates", "pterosaur.json"))).Description);
    }

    [Fact]
    public void ADeclarationThatMovesTheRestOrAContractThatDoesNotHoldIsRefusedWithTheDifferenceNamed()
    {
        var moved = TemplatePublication.Publish(Authored, TemplateResources.Quadruped(false), m => m.Controls.First(c => c.Id == "head").Rest = new(.8f, .86f, 0), Output);
        Assert.False(moved.Published);
        Assert.Contains(moved.Differences, d => d.StartsWith("control 'head' rest moved", StringComparison.Ordinal));
        Assert.False(Directory.Exists(Path.Combine(Output, "models")));

        var template = TemplateResources.Pterosaur();
        template.Slots.Add(new() { Id = "tail-art", Usage = "ribbon" });
        template.Dimensions.Add("neck");
        template.Presets.Add("giant");
        template.Library.Masks.Add("tail");
        template.Library.Mappings["beak"] = "head";
        var broken = TemplatePublication.Publish(Authored, template, PterosaurTemplate.Dimensions, Output);
        Assert.False(broken.Published);
        Assert.Contains("contract: templates > pterosaur > slots > tail-art: model 'pterosaur' has no socket 'tail-art'.", broken.Differences);
        Assert.Contains("contract: templates > pterosaur > dimensions > neck: model 'pterosaur' declares no parameter 'neck'.", broken.Differences);
        Assert.Contains("contract: templates > pterosaur > presets > giant: model 'pterosaur' has no preset 'giant'.", broken.Differences);
        Assert.Contains("contract: templates > pterosaur > library > masks > tail: model 'pterosaur' has no group 'tail'.", broken.Differences);
        Assert.Contains("contract: templates > pterosaur > library > mappings > beak: model 'pterosaur' declares no measure 'beak'.", broken.Differences);

        // A slot's default must be a part of the slot's usage that the model wears there; a role must be assigned by a motion set.
        var person = TemplateResources.Person();
        person.Slots[0].Default = "boot";
        person.Library.Roles["sheathe"] = "idle";
        var catalog = AuthoredCatalog.Load(Authored);
        var problems = person.ContractProblems(catalog.Models["person"], catalog.Parts.GetValueOrDefault);
        Assert.Contains("templates > person > slots > head-art > default: part 'boot' is a 'shoe', the slot takes 'face'.", problems);
        Assert.Contains(problems, p => p.StartsWith("templates > person > slots > head-art > default: model 'person' does not wear 'boot' there", StringComparison.Ordinal));
        Assert.Contains("templates > person > library > roles > sheathe: no motion set of 'person' assigns 'person-idle' to it (path: person > motionSets).", problems);

        // Old versions and mappings onto unexposed dimensions are refused.
        var recipe = TemplateResources.Quadruped(false);
        recipe.Version = ModelTemplate.CurrentVersion - 1;
        Assert.Contains("unsupported format/version", Assert.Throws<InvalidDataException>(recipe.Validate).Message);
        var unexposed = TemplateResources.Quadruped(false);
        unexposed.Library.Mappings["leg"] = "tail";
        Assert.Contains("'tail' is not an exposed dimension", Assert.Throws<InvalidDataException>(unexposed.Validate).Message);
    }

    [Fact]
    public void CreatingFromAPublishedTemplateStartsFromAPresetAndKeepsTheContractOnTheCopy()
    {
        var assets = AuthoringWorkspace.Open(Authored);
        var session = new EditorSession(assets);
        Assert.True(session.NewModel("tall", "Tall quadruped", "quadruped", "long-legged"), session.Message);
        var tall = assets.Model("tall")!.Asset;
        Assert.Equal(1.35f, tall.Parameters.Single(p => p.Id == QuadrupedTemplate.LegsId).Default);
        Assert.Equal(.95f, tall.Parameters.Single(p => p.Id == QuadrupedTemplate.BodyId).Default);
        Assert.Equal(3, tall.Presets.Count);
        var shipped = assets.Resolve("quadruped")!;
        var created = assets.Resolve("tall")!;
        Assert.True(created.Rest["body"].Y > shipped.Rest["body"].Y + .2f, "the long-legged preset lifts the body");
        Assert.Equal(8, assets.Clips.Count(c => c.Asset.Model == "tall"));
        // The copy still satisfies the published contract (slots, dimensions, presets, masks, mappings).
        Assert.Empty(assets.ModelTemplates["quadruped"].ContractProblems(tall, assets.PartOf));
        Assert.False(session.NewModel("giant", "Giant", "quadruped", "giant"));
        Assert.Contains("publishes no preset 'giant' (one of standard, long-legged, stocky)", session.Message);
        // A person from its template wears the face its head slot publishes.
        Assert.True(session.NewModel("clone", "Clone", "person", "short-broad"), session.Message);
        Assert.Equal(ProceduralFace.AssetId, assets.Resolve("clone")!.Attachment(PersonTemplate.FaceInstance)!.Asset.Id);
        Assert.Equal(.82f, assets.Model("clone")!.Asset.Parameters.Single(p => p.Id == PersonBuild.LegsId).Default);
    }
}
