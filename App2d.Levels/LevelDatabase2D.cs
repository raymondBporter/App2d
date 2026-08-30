using App2d.Core;
using App2d.Tiles;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Numerics;

namespace App2d.Levels;

/// <summary>
/// One authored level in one SQLite file. Tiles are run-length encoded per chunk so a
/// single edit rewrites a single row rather than the whole level.
/// </summary>
public sealed partial class LevelDatabase2D : IDisposable
{
    public const int CurrentFormatVersion = 2;

    private readonly SqliteConnection _connection;

    private LevelDatabase2D(SqliteConnection connection) => _connection = connection;

    public int FormatVersion => (int)ReadScalarLong("PRAGMA user_version;");

    public int ChunkRowCount => (int)ReadScalarLong("SELECT COUNT(*) FROM chunks;");

    public static LevelDatabase2D Open(string path)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(path);
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString());
        connection.Open();

        try
        {
            EnableForeignKeys(connection);
            Migrate(connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        return new LevelDatabase2D(connection);
    }

    /// <summary>
    /// Opens an existing level for reading only. Loading must not write: opening
    /// read-write and running DDL bumps SQLite's file change counter, which dirties
    /// the committed level asset in git on every game launch.
    /// </summary>
    public static LevelDatabase2D OpenRead(string path)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(path);
        StateGuard.ThrowIf(!File.Exists(path), $"The level file '{path}' does not exist.");

        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString());
        connection.Open();

        EnableForeignKeys(connection);

        var database = new LevelDatabase2D(connection);
        var version = database.FormatVersion;
        if (version > CurrentFormatVersion)
        {
            connection.Dispose();
            StateGuard.Throw(
                $"The level file is format version {version}, newer than this build understands ({CurrentFormatVersion}).");
        }
        if (version < CurrentFormatVersion)
        {
            connection.Dispose();
            StateGuard.Throw(
                $"The level file is format version {version}; open it for editing once to upgrade it to {CurrentFormatVersion}.");
        }

        return database;
    }

    public void Save(EditableTileMap2D map, ulong sourceSeed)
    {
        ArgGuard.ThrowIfNull(map);

        using var transaction = _connection.BeginTransaction();
        WriteMeta("width", map.Width);
        WriteMeta("height", map.Height);
        WriteMeta("tile_size", map.TileSize);
        WriteMeta("chunk_size", map.ChunkSize);
        WriteMeta("origin_x", map.Origin.X);
        WriteMeta("origin_y", map.Origin.Y);
        WriteMeta("source_seed", sourceSeed.ToString(CultureInfo.InvariantCulture));
        WriteMeta("generated_utc", DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        WriteTilesetCatalog(map.TilesetIds);

        using (var clear = _connection.CreateCommand())
        {
            clear.CommandText = "DELETE FROM chunks;";
            clear.ExecuteNonQuery();
        }

        var buffer = new TileCell2D[map.ChunkSize * map.ChunkSize];
        for (var cy = 0; cy < map.ChunkRows; cy++)
        {
            for (var cx = 0; cx < map.ChunkColumns; cx++)
                WriteChunk(map, new TileChunk2D(cx, cy), buffer);
        }

        transaction.Commit();
    }

    /// <summary>Commits one chunk in its own transaction. A convenience wrapper over <see cref="SaveChunks"/>.</summary>
    public void SaveChunk(EditableTileMap2D map, TileChunk2D chunk) =>
        SaveChunks(map, [chunk]);

    /// <summary>
    /// Commits every given chunk in a single transaction. Microsoft.Data.Sqlite has no
    /// nested transactions, so phase 2's per-stroke write-through -- which can touch several
    /// chunks per stroke -- must call this once per stroke rather than <see cref="SaveChunk"/>
    /// per chunk, or a crash mid-stroke could leave part of the stroke durable while the
    /// in-memory undo stack is gone.
    /// </summary>
    public void SaveChunks(EditableTileMap2D map, ReadOnlySpan<TileChunk2D> chunks)
    {
        ArgGuard.ThrowIfNull(map);
        var buffer = new TileCell2D[map.ChunkSize * map.ChunkSize];
        using var transaction = _connection.BeginTransaction();
        WriteTilesetCatalog(map.TilesetIds);
        foreach (var chunk in chunks)
            WriteChunk(map, chunk, buffer);
        transaction.Commit();
    }

    public EditableTileMap2D Load(IReadOnlyList<string>? legacyTilesetIds = null)
    {
        var width = (int)RequireMetaLong("width");
        var height = (int)RequireMetaLong("height");
        var chunkSize = (int)RequireMetaLong("chunk_size");
        var tileSize = RequireMetaFloat("tile_size");
        var origin = new Vector2(RequireMetaFloat("origin_x"), RequireMetaFloat("origin_y"));

        var tilesetIds = ReadTilesetCatalog() ?? legacyTilesetIds ?? ["default"];
        var map = new EditableTileMap2D(width, height, tileSize, chunkSize, origin, tilesetIds);
        var buffer = new TileCell2D[chunkSize * chunkSize];

        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT cx, cy, tiles FROM chunks;";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var chunk = new TileChunk2D(reader.GetInt32(0), reader.GetInt32(1));
            var encoded = (byte[])reader["tiles"];
            var tileCount = map.ChunkWidth(chunk.X) * map.ChunkHeight(chunk.Y);
            var tiles = buffer.AsSpan(0, tileCount);
            TileCellRunCodec2D.Decode(encoded, tiles);
            map.SetChunkCells(chunk, tiles);
        }

        return map;
    }

    public void Dispose() => _connection.Dispose();

    private static void EnableForeignKeys(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();
    }

    private static void Migrate(SqliteConnection connection)
    {
        long version;
        using (var versionCommand = connection.CreateCommand())
        {
            versionCommand.CommandText = "PRAGMA user_version;";
            version = Convert.ToInt64(versionCommand.ExecuteScalar(), CultureInfo.InvariantCulture);
        }

        if (version > CurrentFormatVersion)
        {
            StateGuard.Throw(
                $"The level file's format version ({version}) is newer than " +
                $"this build supports ({CurrentFormatVersion}).");
        }

        using var transaction = connection.BeginTransaction();
        if (version == 0)
        {
            using var createBase = connection.CreateCommand();
            createBase.CommandText = """
                CREATE TABLE IF NOT EXISTS meta(
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL) WITHOUT ROWID;
                CREATE TABLE IF NOT EXISTS chunks(
                    cx INTEGER NOT NULL,
                    cy INTEGER NOT NULL,
                    tiles BLOB NOT NULL,
                    PRIMARY KEY(cx, cy)) WITHOUT ROWID;
                """;
            createBase.ExecuteNonQuery();
            version = 1;
        }

        if (version == 1)
        {
            using var createThings = connection.CreateCommand();
            createThings.CommandText = ThingSchemaSql;
            createThings.ExecuteNonQuery();
            version = 2;
        }

        using (var stamp = connection.CreateCommand())
        {
            stamp.CommandText = $"PRAGMA user_version = {CurrentFormatVersion};";
            stamp.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private void WriteChunk(EditableTileMap2D map, TileChunk2D chunk, TileCell2D[] buffer)
    {
        var tiles = map.GetChunkCells(chunk, buffer);

        // A missing row means an entirely empty chunk, so empty sky costs no rows.
        var isEmpty = true;
        foreach (var tile in tiles)
        {
            if (tile.Kind == TileKind2D.Empty)
                continue;
            isEmpty = false;
            break;
        }

        using var command = _connection.CreateCommand();
        if (isEmpty)
        {
            command.CommandText = "DELETE FROM chunks WHERE cx = $cx AND cy = $cy;";
        }
        else
        {
            command.CommandText = """
                INSERT INTO chunks(cx, cy, tiles) VALUES($cx, $cy, $tiles)
                ON CONFLICT(cx, cy) DO UPDATE SET tiles = excluded.tiles;
                """;
            command.Parameters.AddWithValue("$tiles", TileCellRunCodec2D.Encode(tiles));
        }

        command.Parameters.AddWithValue("$cx", chunk.X);
        command.Parameters.AddWithValue("$cy", chunk.Y);
        command.ExecuteNonQuery();
    }

    private void WriteMeta(string key, int value) =>
        WriteMeta(key, value.ToString(CultureInfo.InvariantCulture));

    private void WriteMeta(string key, float value) =>
        WriteMeta(key, value.ToString("R", CultureInfo.InvariantCulture));

    private void WriteMeta(string key, string value)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO meta(key, value) VALUES($key, $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    private void WriteTilesetCatalog(IReadOnlyList<string> tilesetIds)
    {
        WriteMeta("tileset_count", tilesetIds.Count);
        for (var index = 0; index < tilesetIds.Count; index++)
            WriteMeta($"tileset_{index}", tilesetIds[index]);
    }

    private string[]? ReadTilesetCatalog()
    {
        using var countCommand = _connection.CreateCommand();
        countCommand.CommandText = "SELECT value FROM meta WHERE key = 'tileset_count';";
        if (countCommand.ExecuteScalar() is not string countText)
            return null;

        var count = int.Parse(countText, CultureInfo.InvariantCulture);
        if (count <= 0 || count > TileCell2D.MaximumTilesetCount)
            throw new InvalidDataException($"Level tileset count {count} is outside the supported range.");

        var ids = new string[count];
        for (var index = 0; index < count; index++)
            ids[index] = RequireMeta($"tileset_{index}");
        return ids;
    }

    private string RequireMeta(string key)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT value FROM meta WHERE key = $key;";
        command.Parameters.AddWithValue("$key", key);
        return StateGuard.RequireNotNull(
            command.ExecuteScalar() as string,
            $"The level file is missing required metadata '{key}'.");
    }

    private long RequireMetaLong(string key) =>
        long.Parse(RequireMeta(key), CultureInfo.InvariantCulture);

    private float RequireMetaFloat(string key) =>
        float.Parse(RequireMeta(key), CultureInfo.InvariantCulture);

    private long ReadScalarLong(string sql)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }
}
