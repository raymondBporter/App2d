using System.Text.RegularExpressions;
using App2d.Core.Characters;

namespace App2d.Tests.Authored;

public sealed class AuthoredCatalogTests
{
    [Fact]
    public void CheckedInAssetsLoadCleanly()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        Assert.True(catalog.Errors.Count == 0, string.Join("\n", catalog.Errors));
        Assert.Equal(new[] { "person", "stalker" }, catalog.Models.Keys.Order());
        Assert.Equal(new[] { "short-broad", "tall-thin" }, catalog.Variants.Keys.Order());
        Assert.Equal(new[]
        {
            "person-idle", "person-jump", "person-run", "person-thrust", "person-walk",
            "player-balance-backward", "player-balance-forward", "player-celebrate", "player-climb", "player-dash", "player-death", "player-fall",
            "player-gun-aim", "player-gun-shot", "player-gun-wall-shot", "player-hit", "player-idle", "player-jump", "player-land",
            "player-sword-down-attack", "player-sword-draw-slash", "player-sword-sheathe", "player-sword-slash", "player-wall-grip",
            "stalker-idle", "stalker-lunge", "stalker-walk",
        }, catalog.Animations.Keys.Order());
        Assert.Equal(new[] { "pistol", "sheath", "spear", "sword" }, catalog.Props.Keys.Order());
        Assert.Equal(new[] { "player", "spear-guard", "stalker-pest" }, catalog.Entities.Keys.Order());
        Assert.Same(catalog.Resolve("tall-thin"), catalog.Resolve("tall-thin"));
        // The compatibility contract is visible in every file, even at its default value.
        foreach (var file in new[] { "models/person.json", "animations/person-walk.json", "animations/person-run.json" })
            Assert.Contains("\"structureRevision\": 1", File.ReadAllText(Path.Combine(TestModels.AuthoredRoot, file)));
    }

    public static TheoryData<string, string, string> NullReferences => new()
    {
        { "models/person.json", "\"root\": \"[^\"]*\"", "\"root\": null" },
        { "models/person.json", "\"frame\": \"[^\"]*\"", "\"frame\": null" },
        { "models/person.json", "\"path\": \\[\\s*\"[^\"]*\"", "\"path\": [ null" },
        { "models/person.json", "\"a\": \"[^\"]*\"", "\"a\": null" },
        { "models/person.json", "\"face\": \"[^\"]*\"", "\"face\": null" },
        { "animations/person-walk.json", "\"scale\": \"leg\"", "\"scale\": null" },
        { "animations/person-walk.json", "\"target\": \"[^\"]*\"", "\"target\": null" },
        { "animations/person-walk.json", "\"chain\": \"[^\"]*\"", "\"chain\": null" },
    };

    [Theory, MemberData(nameof(NullReferences))]
    public void ANullReferenceInAHandEditedFileIsReportedAgainstThatFile(string file, string pattern, string replacement)
    {
        var root = Path.Combine(Path.GetTempPath(), $"authored-{Guid.NewGuid():N}");
        try
        {
            PersonTemplate.WriteStudies(root);
            var path = Path.Combine(root, file); var json = File.ReadAllText(path);
            var edited = new Regex(pattern).Replace(json, replacement, 1);
            Assert.NotEqual(json, edited);
            File.WriteAllText(path, edited);
            var catalog = AuthoredCatalog.Load(root);
            Assert.Contains(catalog.Errors, e => e.Contains(Path.GetFileName(file)));
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory, InlineData("person-walk"), InlineData("person-run")]
    public void CheckedInClipsReproduceThePrototype(string id)
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        var puppet = id == "person-walk" ? PuppetTemplates.StepStudy() : PuppetTemplates.RunStudy();
        TestModels.AssertMatchesPrototype(puppet, catalog.Resolve("person"), catalog.Animations[id]);
    }

    [Fact]
    public void BrokenFilesAreReportedAgainstTheirPathWithoutStoppingTheScan()
    {
        var root = Path.Combine(Path.GetTempPath(), $"authored-{Guid.NewGuid():N}");
        try
        {
            PersonTemplate.WriteStudies(root);
            var orphan = PersonBuild.TallThin.Apply(PersonTemplate.Model(), "orphan", "Orphan"); orphan.Base = "hound";
            File.WriteAllText(Path.Combine(root, "variants", "orphan.json"), orphan.ToJson());
            File.WriteAllText(Path.Combine(root, "variants", "copy.json"), File.ReadAllText(Path.Combine(root, "variants", "tall-thin.json")));
            File.WriteAllText(Path.Combine(root, "animations", "typo.json"), File.ReadAllText(Path.Combine(root, "animations", "person-walk.json")).Replace("\"tracks\":", "\"trakcs\":"));
            var catalog = AuthoredCatalog.Load(root);
            Assert.Contains(catalog.Errors, e => e.Contains("orphan.json") && e.Contains("hound"));
            Assert.Contains(catalog.Errors, e => e.Contains("tall-thin") && e.Contains("already used"));
            Assert.Contains(catalog.Errors, e => e.Contains("typo.json"));
            Assert.Contains("person-walk", catalog.Animations.Keys);
            Assert.Throws<InvalidDataException>(() => catalog.Resolve("orphan"));
        }
        finally { Directory.Delete(root, true); }
    }
}
