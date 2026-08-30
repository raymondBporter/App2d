using App2d.Levels;
using App2d.Tiles;
using System.Numerics;

namespace App2d.Tests.Levels;

public sealed class LevelDatabase2DTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "app2d-levels-" + Guid.NewGuid().ToString("N"));

    private string NewPath()
    {
        Directory.CreateDirectory(_directory);
        return Path.Combine(_directory, "level.db");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private static EditableTileMap2D BuildMap()
    {
        var map = new EditableTileMap2D(10, 6, 32f, 4, new Vector2(-512f, -640f));
        map.Fill((x, y) => (x + y) switch
        {
            0 => TileKind2D.Solid | TileKind2D.Grippable,
            1 => TileKind2D.OneWay,
            2 => TileKind2D.Spikes,
            _ => y < 2 ? TileKind2D.Solid : TileKind2D.Empty
        });
        return map;
    }

    [Fact]
    public void SavedMapRoundTripsEveryTile()
    {
        var path = NewPath();
        var original = BuildMap();

        using (var database = LevelDatabase2D.Open(path))
            database.Save(original, sourceSeed: 0xA2D_2026_0823UL);

        using var reopened = LevelDatabase2D.Open(path);
        var loaded = reopened.Load();

        Assert.Equal(original.Width, loaded.Width);
        Assert.Equal(original.Height, loaded.Height);
        Assert.Equal(original.TileSize, loaded.TileSize);
        Assert.Equal(original.ChunkSize, loaded.ChunkSize);
        Assert.Equal(original.Origin, loaded.Origin);

        for (var y = 0; y < original.Height; y++)
        {
            for (var x = 0; x < original.Width; x++)
                Assert.Equal(original.GetTileKind(x, y), loaded.GetTileKind(x, y));
        }
    }

    [Fact]
    public void EmptyChunksAreNotStoredAsRows()
    {
        var path = NewPath();
        // 2x1 chunks; only the left chunk has any content.
        var map = new EditableTileMap2D(8, 4, 32f, 4);
        map.SetTileKind(1, 1, TileKind2D.Solid);

        using var database = LevelDatabase2D.Open(path);
        database.Save(map, sourceSeed: 0UL);

        Assert.Equal(1, database.ChunkRowCount);

        var loaded = database.Load();
        Assert.Equal(TileKind2D.Solid, loaded.GetTileKind(1, 1));
        Assert.Equal(TileKind2D.Empty, loaded.GetTileKind(5, 1));
    }

    [Fact]
    public void SaveChunkUpdatesOnlyThatChunk()
    {
        var path = NewPath();
        var map = BuildMap();

        using var database = LevelDatabase2D.Open(path);
        database.Save(map, sourceSeed: 0UL);

        map.SetTileKind(0, 0, TileKind2D.Spikes);
        database.SaveChunk(map, new TileChunk2D(0, 0));

        var loaded = database.Load();
        Assert.Equal(TileKind2D.Spikes, loaded.GetTileKind(0, 0));
        Assert.Equal(map.GetTileKind(9, 5), loaded.GetTileKind(9, 5));
    }

    [Fact]
    public void OpeningTwiceReusesTheExistingSchema()
    {
        var path = NewPath();

        using (var first = LevelDatabase2D.Open(path))
            first.Save(BuildMap(), sourceSeed: 0UL);

        using var second = LevelDatabase2D.Open(path);
        Assert.Equal(1, second.FormatVersion);
    }

    [Fact]
    public void LoadingBeforeAnySaveThrows()
    {
        var path = NewPath();
        using var database = LevelDatabase2D.Open(path);

        Assert.Throws<InvalidOperationException>(() => database.Load());
    }
}
