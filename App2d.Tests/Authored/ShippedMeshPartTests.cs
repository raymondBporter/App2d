using App2d.Core.Characters.Authored;

namespace App2d.Tests.Authored;

public sealed class ShippedMeshPartTests
{
    /// <summary>Loading rebuilds every mesh from its outline; the rebuilt vertices and triangles must be the ones on disk.</summary>
    [Fact]
    public void EveryShippedPartWithAMeshSavesUnchanged()
    {
        var checkedFiles = 0;
        foreach (var path in Directory.EnumerateFiles(Path.Combine(TestAssetPath.AuthoredRoot, "parts"), "*.json"))
        {
            var text = File.ReadAllText(path);
            if (!text.Contains("\"mesh\"")) continue;
            Assert.True(text.ReplaceLineEndings() == Part.FromJson(text).ToJson().ReplaceLineEndings(), path);
            checkedFiles++;
        }
        Assert.NotEqual(0, checkedFiles);
    }
}
