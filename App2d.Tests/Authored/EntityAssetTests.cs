using App2d.Core.Characters.Authored;
using App2d.Core.Geometry;
using App2d.Core.Geometry.Shapes;

namespace App2d.Tests.Authored;

public sealed class EntityAssetTests
{
    [Fact]
    public void EveryShippedEntityLoadsInTheCurrentFormat()
    {
        var folder = Path.Combine(TestModels.AuthoredRoot, "entities");
        var paths = Directory.GetFiles(folder, "*.json");
        Assert.NotEmpty(paths);
        foreach (var path in paths)
        {
            var text = File.ReadAllText(path);
            var entity = EntityAsset.FromJson(text);
            // P11c: one version, nothing but declared colliders and typed behaviors; the file is what the asset writes.
            Assert.Equal(EntityAsset.CurrentVersion, entity.Version);
            Assert.Equal(text, entity.ToJson());
            Assert.All(entity.Actions.SelectMany(a => a.Hits), h => Assert.Contains(entity.Colliders, c => c.Id == h.Collider && c.Use == ColliderUses.Attack));
            Assert.IsType<IConvexShape2D>(entity.Movement.Shape.Validated(), exactMatch: false);
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
        var error = Assert.Throws<InvalidDataException>(() => EntityAsset.FromJson(json));
        Assert.Contains("unsupported format/version", error.Message);
    }
}
