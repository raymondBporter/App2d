using App2d.Core.Characters;
using App2d.Core.Characters.Authored;
using System.Text.RegularExpressions;

namespace App2d.Tests.Authored;

public sealed class AuthoredCatalogTests
{
    [Fact]
    public void CheckedInAssetsLoadCleanly()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        Assert.True(catalog.Errors.Count == 0, string.Join("\n", catalog.Errors));
        Assert.Equal(expected, catalog.Models.Keys.Order());
        Assert.Equal(["brute", "cinder", "club-caveman-build", "rock-thrower-build", "shield-defender-build", "short-broad", "tall-thin"], catalog.Variants.Keys.Order());
        Assert.Equal(
        [
            "baby-triceratops-brake", "baby-triceratops-charge", "baby-triceratops-death", "baby-triceratops-head-down", "baby-triceratops-hit", "baby-triceratops-idle", "baby-triceratops-recover", "baby-triceratops-run", "baby-triceratops-rush", "baby-triceratops-scrape", "baby-triceratops-walk",
            "club-caveman-slam",
            "person-death", "person-hammer-slam", "person-heavy-walk", "person-hit", "person-idle", "person-jump", "person-pistol-shot", "person-run", "person-thrust", "person-walk",
            "player-balance-backward", "player-balance-forward", "player-celebrate", "player-climb", "player-climb-off", "player-climb-on", "player-dash", "player-death", "player-fall",
            "player-gun-aim", "player-gun-charge", "player-gun-shot", "player-gun-wall-shot", "player-heal-gather", "player-hit", "player-idle", "player-jump", "player-land",
            "player-sword-backhand", "player-sword-down-attack", "player-sword-forehand", "player-sword-put-away", "player-sword-put-away-backhand", "player-sword-sheathe", "player-sword-side-cut", "player-wall-grip",
            "quadruped-brake", "quadruped-head-down", "quadruped-idle", "quadruped-recover", "quadruped-run", "quadruped-rush", "quadruped-scrape", "quadruped-walk",
            "rock-thrower-throw", "shield-defender-bash", "shield-defender-idle", "shield-defender-walk",
            "stalker-death", "stalker-hit", "stalker-idle", "stalker-lunge", "stalker-walk",
            "triceratops-brake", "triceratops-head-down", "triceratops-idle", "triceratops-recover", "triceratops-run", "triceratops-rush", "triceratops-scrape", "triceratops-walk",
        ], catalog.Animations.Keys.Order());
        Assert.Equal(["brute-beard", "brute-hair", "brute-hide-wrap", "cinder-beard", "cinder-hair", "cinder-hide-wrap", "club-caveman-beard", "club-caveman-hair", "club-caveman-hide-wrap",
            "hair-short", "hair-short-back", "hammer", "pistol", "rock-thrower-bag", "rock-thrower-beard", "rock-thrower-hair", "rock-thrower-hide-wrap",
            "scavenged-shell", "sheath", "shield-defender-beard", "shield-defender-hair", "shield-defender-hide-wrap", "spear", "sword", "throwing-rock", "wooden-club"], catalog.Props.Keys.Order());
        Assert.Equal(["baby-triceratops", "cinder-gunner", "club-caveman", "hero", "maul-brute", "player", "rock-thrower", "shield-defender", "spear-guard", "stalker-pest"], catalog.Entities.Keys.Order());
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

    private static readonly string[] expected = ["baby-triceratops-rig", "person", "quadruped", "stalker", "triceratops"];

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
        TestModels.AssertMatchesPrototype(puppet, ResolvedModel.From(PersonTemplate.StudyReference()), catalog.Animations[id]);
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
