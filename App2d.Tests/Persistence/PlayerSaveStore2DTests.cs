using App2d.Persistence;

namespace App2d.Tests.Persistence;

public sealed class PlayerSaveStore2DTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "app2d-save-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void SaveRoundTripsAsSmallReadableJson()
    {
        var store = NewStore();

        Assert.True(store.TrySave(new PlayerSave2D(27, 4)));

        Assert.Equal(new PlayerSave2D(27, 4), store.TryLoad());
        var json = File.ReadAllText(store.Path);
        Assert.Contains("\"savePointId\": 27", json, StringComparison.Ordinal);
        Assert.Contains("\"hitPoints\": 4", json, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingOrMalformedSaveStartsAsNewGame()
    {
        var store = NewStore();
        Assert.Null(store.TryLoad());

        Directory.CreateDirectory(_directory);
        File.WriteAllText(store.Path, "{ definitely not valid json");

        Assert.Null(store.TryLoad());
    }

    [Fact]
    public void InvalidSaveValuesAreRejected()
    {
        Assert.Throws<InvalidOperationException>(() => new PlayerSave2D(0, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerSave2D(1, 0));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private PlayerSaveStore2D NewStore() =>
        new(Path.Combine(_directory, "save.json"));
}
