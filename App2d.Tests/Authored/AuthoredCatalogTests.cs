using App2d.Core.Characters;

namespace App2d.Tests.Authored;

public sealed class AuthoredCatalogTests
{
    [Fact]
    public void CheckedInAssetsLoadCleanly()
    {
        var catalog = AuthoredCatalog.Load(TestModels.AuthoredRoot);
        Assert.Empty(catalog.Errors);
        Assert.Contains("person", catalog.Models.Keys);
        Assert.Equal(new[] { "short-broad", "tall-thin" }, catalog.Variants.Keys.Order());
        Assert.Equal(new[] { "person-run", "person-walk" }, catalog.Animations.Keys.Order());
        Assert.Same(catalog.Resolve("tall-thin"), catalog.Resolve("tall-thin"));
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
