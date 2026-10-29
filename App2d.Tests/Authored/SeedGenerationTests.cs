using App2d.CharacterSeeds;
using App2d.Core.Characters.Authored;

namespace App2d.Tests.Authored;

/// <summary>
/// P11b: the retained seed generators write the canonical formats Studio writes, and explicit generation never overwrites
/// editor-owned content unexpectedly: a differing file is kept and reported unless the run is forced.
/// </summary>
public sealed class SeedGenerationTests
{
    private static readonly AuthoredCatalog Catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);

    [Fact]
    public void GenerationWritesOnceReportsUnchangedKeepsEditsAndReplacesOnlyWhenForced()
    {
        var root = Path.Combine(Path.GetTempPath(), $"seed-generation-{Guid.NewGuid():N}");
        try
        {
            var first = new SeedReport();
            PersonTemplate.WriteStudies(root, first);
            Assert.True(first.Count(SeedReport.Written) > 10, first.ToString());
            Assert.Equal(0, first.Count(SeedReport.Kept) + first.Count(SeedReport.Unchanged));
            // A second run finds everything as it wrote it.
            var second = new SeedReport();
            PersonTemplate.WriteStudies(root, second);
            Assert.Equal(first.Count(SeedReport.Written), second.Count(SeedReport.Unchanged));
            Assert.Equal(0, second.Count(SeedReport.Written) + second.Count(SeedReport.Kept));
            // The author edits the model and a clip; regeneration keeps both and says so, and writes nothing else.
            var modelPath = Path.Combine(root, "models", "person.json");
            var clipPath = Path.Combine(root, "animations", "person-walk.json");
            var model = CharacterModel.Load(modelPath);
            model.Name = "Person (edited)";
            model.Save(modelPath);
            var clip = MotionClip.FromJson(File.ReadAllText(clipPath));
            clip.Name = "Walk (edited)";
            clip.Save(clipPath);
            var third = new SeedReport();
            PersonTemplate.WriteStudies(root, third);
            Assert.True(third.Entries.Where(e => e.Status == SeedReport.Kept).Select(e => e.Path).Order(StringComparer.Ordinal).SequenceEqual([clipPath, modelPath]), string.Join("; ", third.Lines));
            Assert.Equal("Person (edited)", CharacterModel.Load(modelPath).Name);
            Assert.Equal("Walk (edited)", MotionClip.FromJson(File.ReadAllText(clipPath)).Name);
            Assert.Contains("2 kept", third.ToString());
            // Forced, the generated content replaces the edits and the report says which files it replaced.
            var forced = new SeedReport(force: true);
            PersonTemplate.WriteStudies(root, forced);
            Assert.Equal([clipPath, modelPath], forced.Entries.Where(e => e.Status == SeedReport.Replaced).Select(e => e.Path).Order(StringComparer.Ordinal));
            Assert.Equal("Person", CharacterModel.Load(modelPath).Name);
            // The other writers follow the same policy on an existing tree.
            var creatures = new SeedReport();
            QuadrupedTemplate.Write(root, creatures);
            PterosaurTemplate.Write(root, creatures);
            Assert.Equal(creatures.Entries.Count, creatures.Count(SeedReport.Written));
            var again = new SeedReport();
            QuadrupedTemplate.Write(root, again);
            PterosaurTemplate.Write(root, again);
            Assert.Equal(again.Entries.Count, again.Count(SeedReport.Unchanged));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, true);
            }
        }
    }

    [Fact]
    public void TheSeedsWriteTheCanonicalFormatsTheShippedTreeCarries()
    {
        // Entities declare their colliders and behaviors; models declare no hurt layout; the starter entities are the shipped files
        // (the spear guard's authored reach trigger aside), so regeneration into the shipped tree finds them unchanged.
        foreach (var entity in new[] { StarterContent.SpearGuardEntity(), StarterContent.PlayerEntity(), StarterContent.StalkerEntity() })
        {
            Assert.Equal(Part.CurrentVersion, entity.Version);
            Assert.Contains(ColliderUses.Hurt, entity.Behavior!.Colliders.Values);
            Assert.All(entity.Behavior!.Actions.SelectMany(a => a.Hits), h => Assert.NotNull(h.Collider));
        }
        Assert.Equal(StarterContent.PlayerEntity().ToJson(), Catalog.Entities["player"].Asset.ToJson());
        Assert.Equal(StarterContent.StalkerEntity().ToJson(), Catalog.Entities["stalker-pest"].Asset.ToJson());
        var guard = Part.FromJson(Catalog.Entities["spear-guard"].Asset.ToJson());
        guard.RemoveColliders(ColliderUses.Trigger);
        Assert.Equal(StarterContent.SpearGuardEntity().ToJson(), guard.ToJson());
        foreach (var model in new[] { PersonTemplate.Model(face: true), StarterContent.StalkerModel(), QuadrupedTemplate.Model(), PterosaurTemplate.Model() })
        {
            Assert.Equal(CharacterModel.CurrentVersion, model.Version);
            Assert.All(model.IkChains, c => Assert.Equal(RigNodeKinds.Bone, model.Controls.Single(x => x.Id == c.Root).Node));
        }
        // The Person's proof builds are declared presets, and a build that matches a preset applies through it.
        var person = Catalog.Models["person"];
        Assert.Equal(["standard", "tall-thin", "short-broad", "stilted", "squat"], person.Presets.Select(p => p.Id));
        var stilted = PersonBuild.Stilted.Apply(person, "stilted", "Stilted");
        Assert.Equal(person.Presets.Single(p => p.Id == "stilted").Values, stilted.Parameters);
        var resolved = ResolvedModel.From(person, stilted, Catalog.Parts.GetValueOrDefault);
        Assert.Equal(2, resolved.ParameterValues["legs"]);
        // A new entity from the template starts with one hurt box over its rest pose instead of a layout reference.
        var fresh = EntityAuthoring.New(EntityAuthoring.Guard, "fresh", "Fresh", Catalog.Resolve("quadruped"));
        var box = Assert.Single(fresh.Geometry.Where(g => fresh.Behavior!.Colliders.ContainsKey(g.Id)));
        Assert.Equal((ColliderUses.Hurt, "body", CharacterModel.Locomotion), (fresh.Behavior!.Colliders[box.Id], box.A, box.Frame));
    }

    [Fact]
    public void TheSeedPersonIsTheShippedStructure()
    {
        var seed = PersonTemplate.Model(face: true);
        var shipped = Catalog.Models["person"];
        Assert.Equal(shipped.Controls.Select(c => (c.Id, c.Node, c.Parent, c.Link?.Orientation)), seed.Controls.Select(c => (c.Id, c.Node, c.Parent, c.Link?.Orientation)));
        Assert.Equal(shipped.IkChains.Select(c => (c.Id, c.Root, c.Frame)), seed.IkChains.Select(c => (c.Id, c.Root, c.Frame)));
        Assert.Equal(shipped.Parameters.Select(p => (p.Id, p.Bindings.Count)), seed.Parameters.Select(p => (p.Id, p.Bindings.Count)));
        // Every shipped model and animated part is on the final formats: bone constraints only, no retired member (the P11c format conversion report is the record).
        foreach (var model in Catalog.Models.Values)
        {
            Assert.Equal(CharacterModel.CurrentVersion, model.Version);
            Assert.All(model.IkChains, c => Assert.Equal(RigNodeKinds.Bone, model.Controls.Single(x => x.Id == c.Root).Node));
            Assert.Equal(model.ToJson(), File.ReadAllText(Catalog.PathOf(model.Id)));
        }
        Assert.All(Catalog.Parts.Values, p => Assert.Equal(Part.CurrentVersion, p.Version));
        Assert.All(Catalog.Entities.Values, e => Assert.Equal(e.Asset.ToJson(), File.ReadAllText(Catalog.PathOf(e.Id))));
    }
}
