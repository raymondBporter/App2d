using App2d.Editor;

namespace App2d.Tests.Editor;

public sealed class ObjectAssetCatalog2DTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "app2d-object-catalog-" + Guid.NewGuid().ToString("N"));

    public ObjectAssetCatalog2DTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "objects"));
        var lift = Path.Combine(TestAssetPath.StaticRoot, "levels", "cavern", "objects", "lift.json");
        File.Copy(lift, Path.Combine(_root, "objects", "b.json"));
        File.Copy(lift, Path.Combine(_root, "objects", "a.json"));
        File.WriteAllText(Path.Combine(_root, "objects", "notes.txt"), "not an asset");
    }

    [Fact]
    public void CatalogListsJsonAssetsInOrderAndCachesLoads()
    {
        var catalog = new ObjectAssetCatalog2D(_root);
        Assert.Equal(["objects/a.json", "objects/b.json"], catalog.Paths);
        Assert.Same(catalog.Load("objects/a.json"), catalog.Load("objects/a.json"));
        File.Copy(Path.Combine(_root, "objects", "a.json"), Path.Combine(_root, "objects", "c.json"));
        catalog.Refresh();
        Assert.Equal(3, catalog.Paths.Count);
        Assert.Throws<ArgumentException>(() => catalog.Load("../a.json"));
    }

    [Fact]
    public void SharedPartsLoadFromTheCharacterLibraryAndStayWithinItsPartsFolder()
    {
        var catalog = new ObjectAssetCatalog2D(_root);
        var part = catalog.Load("parts/dumbbell.json");
        Assert.Equal("dumbbell", part.Id);
        Assert.Same(part, catalog.Load("parts/dumbbell.json"));
        Assert.Throws<ArgumentException>(() => catalog.Load("parts/../models/person.json"));
    }

    [Fact]
    public void AFailedLoadIsRememberedUntilTheCatalogRefreshes()
    {
        var broken = Path.Combine(_root, "objects", "broken.json");
        File.WriteAllText(broken, "{ \"format\": \"app2d-object\", \"id\": \"broken\" }");
        var catalog = new ObjectAssetCatalog2D(_root);
        Assert.False(catalog.TryLoad("objects/broken.json", out _));
        var first = Record.Exception(() => catalog.Load("objects/broken.json"));
        Assert.NotNull(first);

        // The file is fixed on disk, but the catalog answers from memory until it is refreshed: no re-read per frame.
        File.Copy(Path.Combine(_root, "objects", "a.json"), broken, overwrite: true);
        Assert.False(catalog.TryLoad("objects/broken.json", out _));
        Assert.IsType(first.GetType(), Record.Exception(() => catalog.Load("objects/broken.json")));

        catalog.Refresh();
        Assert.True(catalog.TryLoad("objects/broken.json", out var repaired));
        Assert.NotNull(repaired);
    }

    [Fact]
    public void MissingObjectsFolderIsAnEmptyCatalog()
    {
        var catalog = new ObjectAssetCatalog2D(Path.Combine(_root, "elsewhere"));
        Assert.Empty(catalog.Paths);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
