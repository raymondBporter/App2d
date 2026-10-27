using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Shapes;

namespace App2d.Tests.Authored;

public sealed class PartTests
{
    [Fact]
    public void EveryShippedEntityLoadsInTheCurrentFormat()
    {
        var folder = Path.Combine(TestModels.AuthoredRoot, "parts");
        var paths = Directory.GetFiles(folder, "*.json");
        Assert.NotEmpty(paths);
        foreach (var path in paths)
        {
            var text = File.ReadAllText(path);
            var entity = Part.FromJson(text);
            if (entity.Behavior is null) continue;
            // P11c: one version, nothing but declared colliders and typed behaviors; the file is what the asset writes.
            Assert.Equal(Part.CurrentVersion, entity.Version);
            Assert.Equal(text, entity.ToJson());
            Assert.All(entity.Behavior!.Actions.SelectMany(a => a.Hits), h => Assert.Equal(ColliderUses.Attack, entity.Behavior.Colliders[h.Collider]));
            Assert.IsType<IConvexShape2D>(((IShape2D)entity.Geometry.Single(g => g.Id == entity.Behavior.Movement).Geometry!).Validated(), exactMatch: false);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(6)]
    public void UnsupportedVersionsAreRejected(int version)
    {
        var json = $$"""{"format":"app2d-entity","version":{{version}}}""";
        var error = Assert.Throws<InvalidDataException>(() => Part.FromJson(json));
        Assert.Contains("unsupported format/version", error.Message);
    }
}
