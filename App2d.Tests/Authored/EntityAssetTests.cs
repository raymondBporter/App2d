using App2d.Core.Characters.Authored;
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
            var entity = EntityAsset.FromJson(File.ReadAllText(path));
            Assert.Equal(EntityAsset.CurrentVersion, entity.Version);
            Assert.IsType<IConvexShape2D>(entity.Movement.Shape.Build(), exactMatch: false);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    public void UnsupportedVersionsAreRejected(int version)
    {
        var json = $$"""{"format":"app2d-entity","version":{{version}}}""";
        var error = Assert.Throws<InvalidDataException>(() => EntityAsset.FromJson(json));
        Assert.Contains("unsupported format/version", error.Message);
    }
}
