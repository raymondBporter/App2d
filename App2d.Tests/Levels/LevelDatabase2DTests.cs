using App2d.Levels;
using App2d.Tiles;
using Microsoft.Data.Sqlite;
using System.Numerics;
using System.Threading;

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
        if (!Directory.Exists(_directory))
            return;

        // Microsoft.Data.Sqlite pools native sqlite3 handles by default: disposing a
        // SqliteConnection returns the handle to the pool rather than closing it, which
        // on Windows keeps the file (and its -wal/-shm siblings, if any) locked for a
        // moment after the last `using` block here has already run. Clear the pool for
        // every connection string used in this directory before deleting, then allow a
        // short bounded retry as a safety margin -- never a blanket catch that would
        // hide a genuine leak.
        ClearPoolsForDatabaseFiles();
        DeleteDirectoryWithRetry(_directory);
    }

    private void ClearPoolsForDatabaseFiles()
    {
        // Tests may create more than one .db file in this directory (e.g. a copy used
        // to verify the committed file is self-contained without its sidecars), so
        // clear the pool for every one rather than assuming a single fixed name. The
        // pool is keyed by the full connection string, so a read-only connection (as
        // used by LevelDatabase2D.OpenRead) pools separately from a read-write one --
        // both must be cleared or a pooled read-only handle keeps the file locked.
        foreach (var dbFile in Directory.EnumerateFiles(_directory, "*.db"))
        {
            foreach (var mode in new[] { SqliteOpenMode.ReadWriteCreate, SqliteOpenMode.ReadOnly })
            {
                using var probe = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = dbFile,
                    Mode = mode
                }.ToString());
                SqliteConnection.ClearPool(probe);
            }
        }
    }

    private static void DeleteDirectoryWithRetry(string directory)
    {
        const int maxAttempts = 10;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                Directory.Delete(directory, recursive: true);
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(20);
            }
        }
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
    public void SaveChunksWritesSeveralChunksInOneTransactionAndRoundTrips()
    {
        var path = NewPath();
        var map = BuildMap();

        using var database = LevelDatabase2D.Open(path);
        database.Save(map, sourceSeed: 0UL);

        map.SetTileKind(0, 0, TileKind2D.Spikes);
        map.SetTileKind(9, 5, TileKind2D.OneWay);
        database.SaveChunks(map, [new TileChunk2D(0, 0), new TileChunk2D(2, 1)]);

        var loaded = database.Load();
        Assert.Equal(TileKind2D.Spikes, loaded.GetTileKind(0, 0));
        Assert.Equal(TileKind2D.OneWay, loaded.GetTileKind(9, 5));
    }

    [Fact]
    public void OpeningTwiceReusesTheExistingSchema()
    {
        var path = NewPath();
        var original = BuildMap();

        using (var first = LevelDatabase2D.Open(path))
            first.Save(original, sourceSeed: 0UL);

        using var second = LevelDatabase2D.Open(path);
        var loaded = second.Load();

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
    public void OpeningAFileStampedToANewerFormatVersionThrowsInsteadOfDowngradingIt()
    {
        var path = NewPath();
        using (var database = LevelDatabase2D.Open(path))
            database.Save(BuildMap(), sourceSeed: 0UL);

        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString()))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText =
                $"PRAGMA user_version = {LevelDatabase2D.CurrentFormatVersion + 1};";
            command.ExecuteNonQuery();
        }

        Assert.Throws<InvalidOperationException>(() => LevelDatabase2D.Open(path));
    }

    [Fact]
    public void LoadingBeforeAnySaveThrows()
    {
        var path = NewPath();
        using var database = LevelDatabase2D.Open(path);

        Assert.Throws<InvalidOperationException>(() => database.Load());
    }

    [Fact]
    public void SavedFileIsSelfContainedWithoutSidecarFiles()
    {
        // Git only ever commits the single .db file -- any -wal/-shm sidecar is
        // gitignored as transient. If the schema ever re-enables WAL mode, the real
        // data can be stranded in a sidecar that Dispose() never checkpoints (Microsoft.Data.Sqlite
        // pools connections, so Dispose does not close the last handle and does not
        // trigger SQLite's checkpoint-on-close). Copying only the .db file reproduces
        // exactly what a fresh clone receives.
        var path = NewPath();
        var original = BuildMap();

        using (var database = LevelDatabase2D.Open(path))
            database.Save(original, sourceSeed: 0xA2D_2026_0823UL);

        var copyPath = Path.Combine(_directory, "level-copy.db");
        File.Copy(path, copyPath);

        using var copy = LevelDatabase2D.Open(copyPath);
        var loaded = copy.Load();

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
    public void OpenReadLoadsAnExistingLevel()
    {
        var path = NewPath();
        var original = BuildMap();
        using (var database = LevelDatabase2D.Open(path))
            database.Save(original, sourceSeed: 0UL);

        using var reader = LevelDatabase2D.OpenRead(path);
        var loaded = reader.Load();

        Assert.Equal(original.Width, loaded.Width);
        for (var y = 0; y < original.Height; y++)
        {
            for (var x = 0; x < original.Width; x++)
                Assert.Equal(original.GetTileKind(x, y), loaded.GetTileKind(x, y));
        }
    }

    [Fact]
    public void OpenReadDoesNotModifyTheFile()
    {
        var path = NewPath();
        using (var database = LevelDatabase2D.Open(path))
            database.Save(BuildMap(), sourceSeed: 0UL);

        // Microsoft.Data.Sqlite pools native handles (see the class Dispose() comment
        // above): the read-write connection just disposed keeps the OS file handle open
        // with write access, which conflicts with File.ReadAllBytes's read-only sharing
        // request. Clear that pool so this reads the real on-disk bytes, not a
        // sharing-violation artifact of pooling.
        SqliteConnection.ClearPool(new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString()));

        var before = File.ReadAllBytes(path);

        using (var reader = LevelDatabase2D.OpenRead(path))
            reader.Load();

        var after = File.ReadAllBytes(path);

        // Opening read-write and running DDL bumps SQLite's file change counter
        // (bytes 28 and 96), which dirties the committed level asset in git on
        // every launch. The read path must leave the bytes untouched.
        Assert.Equal(before, after);
    }

    [Fact]
    public void OpenReadOnAMissingFileThrows()
    {
        Directory.CreateDirectory(_directory);
        var missing = Path.Combine(_directory, "does-not-exist.db");

        Assert.Throws<InvalidOperationException>(() => LevelDatabase2D.OpenRead(missing));
    }

    [Fact]
    public void SavedMapRoundTripsTilesetCatalogAndPackedCells()
    {
        var path = NewPath();
        var map = new EditableTileMap2D(
            4,
            4,
            32f,
            2,
            tilesetIds: ["dark-cave", "mossy-cavern"]);
        map.SetTile(2, 3, new TileCell2D(TileKind2D.Solid | TileKind2D.Grippable, 1));

        using (var database = LevelDatabase2D.Open(path))
            database.Save(map, sourceSeed: 0UL);

        using var reader = LevelDatabase2D.OpenRead(path);
        var loaded = reader.Load();

        Assert.Equal(["dark-cave", "mossy-cavern"], loaded.TilesetIds);
        Assert.Equal(
            new TileCell2D(TileKind2D.Solid | TileKind2D.Grippable, 1),
            loaded.GetTile(2, 3));
    }
}
