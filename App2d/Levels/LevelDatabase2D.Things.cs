using App2d.Core.Validation;
using Microsoft.Data.Sqlite;

namespace App2d.Levels;

public sealed partial class LevelDatabase2D
{
    /// <summary>The thing type whose definition places an authored object file.</summary>
    public const string ObjectTypeKey = "object";

    private const string ThingSchemaSql = """
        CREATE TABLE thing_definitions(
            definition_id INTEGER PRIMARY KEY,
            type_key TEXT NOT NULL,
            name TEXT NOT NULL);
        CREATE TABLE things(
            thing_id INTEGER PRIMARY KEY,
            definition_id INTEGER NOT NULL
                REFERENCES thing_definitions(definition_id) ON DELETE RESTRICT,
            name TEXT,
            enabled INTEGER NOT NULL DEFAULT 1 CHECK(enabled IN (0, 1)));
        CREATE TABLE transforms(
            thing_id INTEGER PRIMARY KEY
                REFERENCES things(thing_id) ON DELETE CASCADE,
            x REAL NOT NULL,
            y REAL NOT NULL,
            rotation REAL NOT NULL DEFAULT 0);
        CREATE INDEX things_definition_id_idx ON things(definition_id);
        """;

    private const string ObjectReferencesSql = """
        CREATE TABLE IF NOT EXISTS object_references(
            definition_id INTEGER PRIMARY KEY
                REFERENCES thing_definitions(definition_id) ON DELETE CASCADE,
            asset TEXT NOT NULL);
        """;

    public IReadOnlyList<ThingDefinitionRecord2D> LoadThingDefinitions()
    {
        var records = new List<ThingDefinitionRecord2D>();
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT d.definition_id, d.type_key, d.name, o.asset
            FROM thing_definitions d
            LEFT JOIN object_references o USING(definition_id)
            ORDER BY d.name, d.definition_id;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            records.Add(new ThingDefinitionRecord2D(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }
        return records;
    }

    public IReadOnlyList<PositionThingRecord2D> LoadPositionThings()
    {
        var records = new List<PositionThingRecord2D>();
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT
                t.thing_id, t.definition_id, d.type_key, d.name, t.name, t.enabled,
                x.x, x.y, x.rotation, o.asset
            FROM things t
            JOIN thing_definitions d USING(definition_id)
            LEFT JOIN transforms x USING(thing_id)
            LEFT JOIN object_references o USING(definition_id)
            ORDER BY t.thing_id;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var thingId = reader.GetInt64(0);
            var typeKey = reader.GetString(2);
            RequireThingPiece(reader, 6, thingId, "transforms", typeKey);
            records.Add(new PositionThingRecord2D(
                thingId,
                reader.GetInt64(1),
                typeKey,
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetInt64(5) != 0,
                reader.GetFloat(6),
                reader.GetFloat(7),
                reader.GetFloat(8),
                reader.IsDBNull(9) ? null : reader.GetString(9)));
        }
        return records;
    }

    /// <summary>An <c>object</c> definition names the asset file it places; every other type takes none.</summary>
    public ThingDefinitionRecord2D CreateThingDefinition(string typeKey, string name, string? asset = null)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(typeKey);
        ArgGuard.ThrowIfNullOrWhiteSpace(name);
        var normalizedAsset = NormalizeAsset(typeKey.Trim(), asset);
        using var transaction = _connection.BeginTransaction();
        long definitionId;
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO thing_definitions(type_key, name)
                VALUES($type_key, $name)
                RETURNING definition_id;
                """;
            command.Parameters.AddWithValue("$type_key", typeKey.Trim());
            command.Parameters.AddWithValue("$name", name.Trim());
            definitionId = Convert.ToInt64(command.ExecuteScalar());
        }
        WriteObjectReference(definitionId, normalizedAsset);
        transaction.Commit();
        return new ThingDefinitionRecord2D(definitionId, typeKey.Trim(), name.Trim(), normalizedAsset);
    }

    public ThingDefinitionRecord2D UpdateThingDefinition(ThingDefinitionRecord2D record)
    {
        ArgGuard.ThrowIfNull(record);
        ArgGuard.ThrowIfNullOrWhiteSpace(record.TypeKey);
        ArgGuard.ThrowIfNullOrWhiteSpace(record.Name);
        var normalizedAsset = NormalizeAsset(record.TypeKey, record.Asset);
        using var transaction = _connection.BeginTransaction();
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = """
                UPDATE thing_definitions SET name = $name
                WHERE definition_id = $definition_id AND type_key = $type_key;
                """;
            command.Parameters.AddWithValue("$name", record.Name.Trim());
            command.Parameters.AddWithValue("$definition_id", record.DefinitionId);
            command.Parameters.AddWithValue("$type_key", record.TypeKey);
            StateGuard.ThrowIf(command.ExecuteNonQuery() != 1, $"Thing definition {record.DefinitionId} of type '{record.TypeKey}' does not exist.");
        }
        WriteObjectReference(record.DefinitionId, normalizedAsset);
        transaction.Commit();
        return record with
        {
            Name = record.Name.Trim(),
            Asset = normalizedAsset
        };
    }

    private static string? NormalizeAsset(string typeKey, string? asset)
    {
        var isObject = string.Equals(typeKey, ObjectTypeKey, StringComparison.Ordinal);
        if (isObject && string.IsNullOrWhiteSpace(asset))
        {
            throw ArgGuard.CreateInvalid("An object definition needs an asset file.", nameof(asset));
        }
        if (!isObject && !string.IsNullOrWhiteSpace(asset))
        {
            throw ArgGuard.CreateInvalid($"Thing type '{typeKey}' does not take an asset file.", nameof(asset));
        }
        return isObject ? asset!.Trim() : null;
    }

    private void WriteObjectReference(long definitionId, string? asset)
    {
        using var command = _connection.CreateCommand();
        if (asset is null)
        {
            command.CommandText = "DELETE FROM object_references WHERE definition_id = $definition_id;";
        }
        else
        {
            command.CommandText = """
                INSERT INTO object_references(definition_id, asset) VALUES($definition_id, $asset)
                ON CONFLICT(definition_id) DO UPDATE SET asset = excluded.asset;
                """;
            command.Parameters.AddWithValue("$asset", asset);
        }
        command.Parameters.AddWithValue("$definition_id", definitionId);
        command.ExecuteNonQuery();
    }

    public void DeleteThingDefinition(long definitionId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM thing_definitions WHERE definition_id = $definition_id;";
        command.Parameters.AddWithValue("$definition_id", definitionId);
        StateGuard.ThrowIf(command.ExecuteNonQuery() != 1, $"Thing definition {definitionId} does not exist.");
    }

    public PositionThingRecord2D CreatePositionThing(NewPositionThing2D input)
    {
        ArgGuard.ThrowIfNull(input);
        ValidateTransform(input.X, input.Y, input.Rotation);
        using var transaction = _connection.BeginTransaction();
        var definition = RequireThingDefinition(input.DefinitionId);
        long thingId;
        using (var thing = _connection.CreateCommand())
        {
            thing.CommandText = """
                INSERT INTO things(definition_id, name, enabled)
                VALUES($definition_id, $name, $enabled)
                RETURNING thing_id;
                """;
            thing.Parameters.AddWithValue("$definition_id", input.DefinitionId);
            thing.Parameters.AddWithValue("$name", (object?)NormalizeOptionalName(input.Name) ?? DBNull.Value);
            thing.Parameters.AddWithValue("$enabled", input.Enabled ? 1 : 0);
            thingId = Convert.ToInt64(thing.ExecuteScalar());
        }
        using (var transform = _connection.CreateCommand())
        {
            transform.CommandText = "INSERT INTO transforms(thing_id, x, y, rotation) VALUES($id, $x, $y, $rotation);";
            transform.Parameters.AddWithValue("$id", thingId);
            transform.Parameters.AddWithValue("$x", input.X);
            transform.Parameters.AddWithValue("$y", input.Y);
            transform.Parameters.AddWithValue("$rotation", input.Rotation);
            transform.ExecuteNonQuery();
        }
        transaction.Commit();
        return new PositionThingRecord2D(
            thingId,
            definition.DefinitionId,
            definition.TypeKey,
            definition.Name,
            NormalizeOptionalName(input.Name),
            input.Enabled,
            input.X,
            input.Y,
            input.Rotation,
            definition.Asset);
    }

    public PositionThingRecord2D UpdatePositionThing(PositionThingRecord2D record)
    {
        ArgGuard.ThrowIfNull(record);
        ValidateTransform(record.X, record.Y, record.Rotation);
        using var transaction = _connection.BeginTransaction();
        var definition = RequireThingDefinition(record.DefinitionId);
        StateGuard.ThrowIf(
            !string.Equals(definition.TypeKey, record.TypeKey, StringComparison.Ordinal),
            $"Thing definition {record.DefinitionId} has type '{definition.TypeKey}', not '{record.TypeKey}'.");
        using (var thing = _connection.CreateCommand())
        {
            thing.CommandText = "UPDATE things SET name = $name, enabled = $enabled WHERE thing_id = $thing_id AND definition_id = $definition_id;";
            thing.Parameters.AddWithValue("$name", (object?)NormalizeOptionalName(record.Name) ?? DBNull.Value);
            thing.Parameters.AddWithValue("$enabled", record.Enabled ? 1 : 0);
            thing.Parameters.AddWithValue("$thing_id", record.ThingId);
            thing.Parameters.AddWithValue("$definition_id", record.DefinitionId);
            StateGuard.ThrowIf(thing.ExecuteNonQuery() != 1, $"Thing {record.ThingId} does not exist.");
        }
        using (var transform = _connection.CreateCommand())
        {
            transform.CommandText = "UPDATE transforms SET x = $x, y = $y, rotation = $rotation WHERE thing_id = $thing_id;";
            transform.Parameters.AddWithValue("$x", record.X);
            transform.Parameters.AddWithValue("$y", record.Y);
            transform.Parameters.AddWithValue("$rotation", record.Rotation);
            transform.Parameters.AddWithValue("$thing_id", record.ThingId);
            StateGuard.ThrowIf(transform.ExecuteNonQuery() != 1, $"Thing {record.ThingId} is missing transforms.");
        }
        transaction.Commit();
        return record with
        {
            DefinitionName = definition.Name,
            Name = NormalizeOptionalName(record.Name),
            Asset = definition.Asset
        };
    }

    public PositionThingRecord2D DeletePositionThing(long thingId)
    {
        var record = LoadPositionThings().SingleOrDefault(item => item.ThingId == thingId);
        StateGuard.ThrowIf(record is null, $"Position thing {thingId} does not exist.");
        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM things WHERE thing_id = $thing_id;";
        command.Parameters.AddWithValue("$thing_id", thingId);
        StateGuard.ThrowIf(command.ExecuteNonQuery() != 1, $"Thing {thingId} does not exist.");
        return record!;
    }

    public void RestorePositionThing(PositionThingRecord2D record)
    {
        ArgGuard.ThrowIfNull(record);
        ValidateTransform(record.X, record.Y, record.Rotation);
        using var transaction = _connection.BeginTransaction();
        RequireThingDefinition(record.DefinitionId);
        using (var thing = _connection.CreateCommand())
        {
            thing.CommandText = "INSERT INTO things(thing_id, definition_id, name, enabled) VALUES($thing_id, $definition_id, $name, $enabled);";
            thing.Parameters.AddWithValue("$thing_id", record.ThingId);
            thing.Parameters.AddWithValue("$definition_id", record.DefinitionId);
            thing.Parameters.AddWithValue("$name", (object?)NormalizeOptionalName(record.Name) ?? DBNull.Value);
            thing.Parameters.AddWithValue("$enabled", record.Enabled ? 1 : 0);
            thing.ExecuteNonQuery();
        }
        using (var transform = _connection.CreateCommand())
        {
            transform.CommandText = "INSERT INTO transforms(thing_id, x, y, rotation) VALUES($thing_id, $x, $y, $rotation);";
            transform.Parameters.AddWithValue("$thing_id", record.ThingId);
            transform.Parameters.AddWithValue("$x", record.X);
            transform.Parameters.AddWithValue("$y", record.Y);
            transform.Parameters.AddWithValue("$rotation", record.Rotation);
            transform.ExecuteNonQuery();
        }
        transaction.Commit();
    }

    private ThingDefinitionRecord2D RequireThingDefinition(long definitionId) =>
        LoadThingDefinitions().SingleOrDefault(item => item.DefinitionId == definitionId) ??
        throw new InvalidOperationException($"Thing definition {definitionId} does not exist.");

    private static void ValidateTransform(float x, float y, float rotation)
    {
        ArgGuard.Finite(x);
        ArgGuard.Finite(y);
        ArgGuard.Finite(rotation);
    }

    private static string? NormalizeOptionalName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    private static void RequireThingPiece(SqliteDataReader reader, int ordinal, long thingId, string table, string typeKey) =>
        StateGuard.ThrowIf(
            reader.IsDBNull(ordinal),
            $"Thing {thingId} of type '{typeKey}' is missing required piece '{table}'.");
}
