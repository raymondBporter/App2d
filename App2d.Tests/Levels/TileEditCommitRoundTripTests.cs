using App2d.Levels;
using App2d.Tiles;
using Microsoft.Data.Sqlite;
using System.Numerics;
using System.Threading;

namespace App2d.Tests.Levels;

/// <summary>
/// End-to-end coverage for the tile painter's write-through path: a stroke recorded by
/// <see cref="TileEditSession2D"/>, committed through <see cref="LevelDatabase2D.SaveChunks"/>,
/// and reloaded from disk with <see cref="LevelDatabase2D.OpenRead"/>. The unit tests for the
/// session and for the database each pass in isolation, but two silent-data-loss bugs were
/// found in review of the code that wires them together (a stroke abandoned by a second mouse
/// button, and a stroke orphaned by focus loss) -- so the commit path itself, session output
/// straight into storage and back, needs its own test rather than trusting the pieces compose
/// correctly.
/// </summary>
public sealed class TileEditCommitRoundTripTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "app2d-edit-commit-" + Guid.NewGuid().ToString("N"));

    private string NewPath()
    {
        Directory.CreateDirectory(_directory);
        return Path.Combine(_directory, "level.db");
    }

    public void Dispose()
    {
        if (!Directory.Exists(_directory))
            return;

        // See LevelDatabase2DTests.Dispose for why pools must be cleared before deleting:
        // Microsoft.Data.Sqlite pools native handles, which can keep the file locked for a
        // moment after the last `using` block here has already run.
        ClearPoolsForDatabaseFiles();
        DeleteDirectoryWithRetry(_directory);
    }

    private void ClearPoolsForDatabaseFiles()
    {
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

    [Fact]
    public void StrokeCommittedThroughSaveChunksRoundTripsEveryPaintedTile()
    {
        var path = NewPath();
        var map = new EditableTileMap2D(16, 16, 32f, 4, new Vector2(-512f, -640f));

        // A real editing session always starts from an already-baked file: SaveChunks
        // writes only the chunks table, so a prior full Save is what supplies the meta
        // rows (width/height/tile_size/...) that Load() requires.
        using (var bake = LevelDatabase2D.Open(path))
            bake.Save(map, sourceSeed: 0UL);

        var session = new TileEditSession2D(map);
        session.BeginStroke();
        session.Paint(3, 3, TileKind2D.Solid);   // a chunk corner: touches (0,0) only for data
        session.Paint(4, 3, TileKind2D.OneWay);  // adjacent chunk column: (1,0)
        session.Paint(10, 10, TileKind2D.Spikes); // far chunk: (2,2)
        var chunks = session.EndStroke();

        using (var edit = LevelDatabase2D.Open(path))
            edit.SaveChunks(map, chunks.ToArray());

        using var reader = LevelDatabase2D.OpenRead(path);
        var loaded = reader.Load();

        Assert.Equal(TileKind2D.Solid, loaded.GetTileKind(3, 3));
        Assert.Equal(TileKind2D.OneWay, loaded.GetTileKind(4, 3));
        Assert.Equal(TileKind2D.Spikes, loaded.GetTileKind(10, 10));
    }

    [Fact]
    public void StrokeThatErasesAChunkBackToEmptyRoundTripsAsEmpty()
    {
        // This is the WriteChunk "DELETE FROM chunks" branch: a chunk that had content
        // loses all of it in one stroke. If the delete silently failed to happen (or
        // happened against the wrong row), the stale row would resurrect the "erased"
        // tiles on reload -- exactly the kind of loss the design doc warns WriteChunk's
        // whole-chunk persistence can hide.
        var path = NewPath();
        var map = new EditableTileMap2D(8, 8, 32f, 4, new Vector2(-512f, -640f));
        map.SetTileKind(1, 1, TileKind2D.Solid);
        map.SetTileKind(2, 2, TileKind2D.OneWay);

        using (var bake = LevelDatabase2D.Open(path))
            bake.Save(map, sourceSeed: 0UL);

        var session = new TileEditSession2D(map);
        session.BeginStroke();
        session.Paint(1, 1, TileKind2D.Empty);
        session.Paint(2, 2, TileKind2D.Empty);
        var chunks = session.EndStroke();

        using (var edit = LevelDatabase2D.Open(path))
            edit.SaveChunks(map, chunks.ToArray());

        using var reader = LevelDatabase2D.OpenRead(path);

        // A missing row IS the encoding for an empty chunk (see WriteChunk), so the
        // erased chunk's row must be gone rather than merely zeroed.
        Assert.Equal(0, reader.ChunkRowCount);

        var loaded = reader.Load();
        Assert.Equal(TileKind2D.Empty, loaded.GetTileKind(1, 1));
        Assert.Equal(TileKind2D.Empty, loaded.GetTileKind(2, 2));
    }
}
