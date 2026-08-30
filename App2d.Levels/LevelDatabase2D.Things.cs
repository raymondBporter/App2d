using App2d.Core;
using Microsoft.Data.Sqlite;

namespace App2d.Levels;

public sealed partial class LevelDatabase2D
{
    private const string MovingPlatformTypeKey = "moving-platform";

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
        CREATE TABLE rectangle_shapes(
            definition_id INTEGER PRIMARY KEY
                REFERENCES thing_definitions(definition_id) ON DELETE CASCADE,
            width REAL NOT NULL CHECK(width > 0),
            height REAL NOT NULL CHECK(height > 0));
        CREATE TABLE solid_color_art(
            definition_id INTEGER PRIMARY KEY
                REFERENCES thing_definitions(definition_id) ON DELETE CASCADE,
            color_argb INTEGER NOT NULL);
        CREATE TABLE transforms(
            thing_id INTEGER PRIMARY KEY
                REFERENCES things(thing_id) ON DELETE CASCADE,
            x REAL NOT NULL,
            y REAL NOT NULL,
            rotation REAL NOT NULL DEFAULT 0);
        CREATE TABLE ping_pong_motors(
            thing_id INTEGER PRIMARY KEY
                REFERENCES things(thing_id) ON DELETE CASCADE,
            travel_x REAL NOT NULL,
            travel_y REAL NOT NULL,
            speed REAL NOT NULL CHECK(speed > 0),
            CHECK(travel_x <> 0 OR travel_y <> 0));
        CREATE INDEX things_definition_id_idx ON things(definition_id);
        """;

    public IReadOnlyList<ThingDefinitionRecord2D> LoadThingDefinitions()
    {
        var records = new List<ThingDefinitionRecord2D>();
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT definition_id, type_key, name
            FROM thing_definitions
            ORDER BY name, definition_id;
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            records.Add(new ThingDefinitionRecord2D(
                reader.GetInt64(0),
                reader.GetString(1),
                reader.GetString(2)));
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
                x.x, x.y, x.rotation
            FROM things t
            JOIN thing_definitions d USING(definition_id)
            LEFT JOIN transforms x USING(thing_id)
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
                reader.GetFloat(8)));
        }
        return records;
    }

    public ThingDefinitionRecord2D CreateThingDefinition(string typeKey, string name)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(typeKey);
        ArgGuard.ThrowIfNullOrWhiteSpace(name);
        using var command = _connection.CreateCommand();
        command.CommandText = """
            INSERT INTO thing_definitions(type_key, name)
            VALUES($type_key, $name)
            RETURNING definition_id;
            """;
        command.Parameters.AddWithValue("$type_key", typeKey.Trim());
        command.Parameters.AddWithValue("$name", name.Trim());
        var definitionId = Convert.ToInt64(command.ExecuteScalar());
        return new ThingDefinitionRecord2D(definitionId, typeKey.Trim(), name.Trim());
    }

    public ThingDefinitionRecord2D UpdateThingDefinition(ThingDefinitionRecord2D record)
    {
        ArgGuard.ThrowIfNull(record);
        ArgGuard.ThrowIfNullOrWhiteSpace(record.TypeKey);
        ArgGuard.ThrowIfNullOrWhiteSpace(record.Name);
        using var command = _connection.CreateCommand();
        command.CommandText = """
            UPDATE thing_definitions SET name = $name
            WHERE definition_id = $definition_id AND type_key = $type_key;
            """;
        command.Parameters.AddWithValue("$name", record.Name.Trim());
        command.Parameters.AddWithValue("$definition_id", record.DefinitionId);
        command.Parameters.AddWithValue("$type_key", record.TypeKey);
        StateGuard.ThrowIf(command.ExecuteNonQuery() != 1, $"Thing definition {record.DefinitionId} of type '{record.TypeKey}' does not exist.");
        return record with { Name = record.Name.Trim() };
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
            input.Rotation);
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
        return record with { DefinitionName = definition.Name, Name = NormalizeOptionalName(record.Name) };
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

    public IReadOnlyList<MovingPlatformDefinitionRecord2D> LoadMovingPlatformDefinitions()
    {
        var records = new List<MovingPlatformDefinitionRecord2D>();
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT d.definition_id, d.name, s.width, s.height, a.color_argb
            FROM thing_definitions d
            LEFT JOIN rectangle_shapes s USING(definition_id)
            LEFT JOIN solid_color_art a USING(definition_id)
            WHERE d.type_key = $type_key
            ORDER BY d.name, d.definition_id;
            """;
        command.Parameters.AddWithValue("$type_key", MovingPlatformTypeKey);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var definitionId = reader.GetInt64(0);
            RequirePiece(reader, 2, definitionId, "rectangle_shapes");
            RequirePiece(reader, 4, definitionId, "solid_color_art");
            records.Add(new MovingPlatformDefinitionRecord2D(
                definitionId,
                reader.GetString(1),
                reader.GetFloat(2),
                reader.GetFloat(3),
                reader.GetInt32(4)));
        }
        return records;
    }

    public IReadOnlyList<MovingPlatformThingRecord2D> LoadMovingPlatforms()
    {
        var records = new List<MovingPlatformThingRecord2D>();
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT
                t.thing_id, t.definition_id, d.name, t.name, t.enabled,
                s.width, s.height, a.color_argb,
                x.x, x.y, x.rotation,
                m.travel_x, m.travel_y, m.speed
            FROM things t
            JOIN thing_definitions d USING(definition_id)
            LEFT JOIN rectangle_shapes s USING(definition_id)
            LEFT JOIN solid_color_art a USING(definition_id)
            LEFT JOIN transforms x USING(thing_id)
            LEFT JOIN ping_pong_motors m USING(thing_id)
            WHERE d.type_key = $type_key
            ORDER BY t.thing_id;
            """;
        command.Parameters.AddWithValue("$type_key", MovingPlatformTypeKey);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var thingId = reader.GetInt64(0);
            var definitionId = reader.GetInt64(1);
            RequirePiece(reader, 5, definitionId, "rectangle_shapes");
            RequirePiece(reader, 7, definitionId, "solid_color_art");
            RequireThingPiece(reader, 8, thingId, "transforms");
            RequireThingPiece(reader, 11, thingId, "ping_pong_motors");
            records.Add(new MovingPlatformThingRecord2D(
                thingId,
                definitionId,
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt64(4) != 0,
                reader.GetFloat(5),
                reader.GetFloat(6),
                reader.GetInt32(7),
                reader.GetFloat(8),
                reader.GetFloat(9),
                reader.GetFloat(10),
                reader.GetFloat(11),
                reader.GetFloat(12),
                reader.GetFloat(13)));
        }
        return records;
    }

    public MovingPlatformDefinitionRecord2D CreateMovingPlatformDefinition(
        string name,
        float width,
        float height,
        int colorArgb)
    {
        ValidateDefinition(name, width, height);
        using var transaction = _connection.BeginTransaction();
        long definitionId;
        using (var definition = _connection.CreateCommand())
        {
            definition.CommandText = """
                INSERT INTO thing_definitions(type_key, name)
                VALUES($type_key, $name)
                RETURNING definition_id;
                """;
            definition.Parameters.AddWithValue("$type_key", MovingPlatformTypeKey);
            definition.Parameters.AddWithValue("$name", name.Trim());
            definitionId = Convert.ToInt64(definition.ExecuteScalar());
        }
        InsertDefinitionPieces(definitionId, width, height, colorArgb);
        transaction.Commit();
        return new MovingPlatformDefinitionRecord2D(definitionId, name.Trim(), width, height, colorArgb);
    }

    public MovingPlatformDefinitionRecord2D UpdateMovingPlatformDefinition(
        MovingPlatformDefinitionRecord2D record)
    {
        ArgGuard.ThrowIfNull(record);
        ValidateDefinition(record.Name, record.Width, record.Height);
        using var transaction = _connection.BeginTransaction();
        using (var definition = _connection.CreateCommand())
        {
            definition.CommandText = """
                UPDATE thing_definitions SET name = $name
                WHERE definition_id = $definition_id AND type_key = $type_key;
                """;
            definition.Parameters.AddWithValue("$name", record.Name.Trim());
            definition.Parameters.AddWithValue("$definition_id", record.DefinitionId);
            definition.Parameters.AddWithValue("$type_key", MovingPlatformTypeKey);
            StateGuard.ThrowIf(definition.ExecuteNonQuery() != 1, $"Moving-platform definition {record.DefinitionId} does not exist.");
        }
        using (var shape = _connection.CreateCommand())
        {
            shape.CommandText = "UPDATE rectangle_shapes SET width = $width, height = $height WHERE definition_id = $definition_id;";
            shape.Parameters.AddWithValue("$width", record.Width);
            shape.Parameters.AddWithValue("$height", record.Height);
            shape.Parameters.AddWithValue("$definition_id", record.DefinitionId);
            StateGuard.ThrowIf(shape.ExecuteNonQuery() != 1, $"Moving-platform definition {record.DefinitionId} is missing rectangle_shapes.");
        }
        using (var art = _connection.CreateCommand())
        {
            art.CommandText = "UPDATE solid_color_art SET color_argb = $color WHERE definition_id = $definition_id;";
            art.Parameters.AddWithValue("$color", record.ColorArgb);
            art.Parameters.AddWithValue("$definition_id", record.DefinitionId);
            StateGuard.ThrowIf(art.ExecuteNonQuery() != 1, $"Moving-platform definition {record.DefinitionId} is missing solid_color_art.");
        }
        transaction.Commit();
        return record with { Name = record.Name.Trim() };
    }

    public void DeleteMovingPlatformDefinition(long definitionId)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM thing_definitions WHERE definition_id = $definition_id AND type_key = $type_key;";
        command.Parameters.AddWithValue("$definition_id", definitionId);
        command.Parameters.AddWithValue("$type_key", MovingPlatformTypeKey);
        StateGuard.ThrowIf(command.ExecuteNonQuery() != 1, $"Moving-platform definition {definitionId} does not exist.");
    }

    public MovingPlatformThingRecord2D CreateMovingPlatform(NewMovingPlatformThing2D input)
    {
        ArgGuard.ThrowIfNull(input);
        ValidateThing(input.X, input.Y, input.Rotation, input.TravelX, input.TravelY, input.Speed);
        using var transaction = _connection.BeginTransaction();
        var definition = RequireMovingPlatformDefinition(input.DefinitionId);
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
        InsertThingPieces(thingId, input.X, input.Y, input.Rotation, input.TravelX, input.TravelY, input.Speed);
        transaction.Commit();
        return ResolveThing(thingId, definition, input);
    }

    public MovingPlatformThingRecord2D UpdateMovingPlatform(MovingPlatformThingRecord2D record)
    {
        ArgGuard.ThrowIfNull(record);
        ValidateThing(record.X, record.Y, record.Rotation, record.TravelX, record.TravelY, record.Speed);
        using var transaction = _connection.BeginTransaction();
        var definition = RequireMovingPlatformDefinition(record.DefinitionId);
        using (var thing = _connection.CreateCommand())
        {
            thing.CommandText = """
                UPDATE things SET definition_id = $definition_id, name = $name, enabled = $enabled
                WHERE thing_id = $thing_id;
                """;
            thing.Parameters.AddWithValue("$definition_id", record.DefinitionId);
            thing.Parameters.AddWithValue("$name", (object?)NormalizeOptionalName(record.Name) ?? DBNull.Value);
            thing.Parameters.AddWithValue("$enabled", record.Enabled ? 1 : 0);
            thing.Parameters.AddWithValue("$thing_id", record.ThingId);
            StateGuard.ThrowIf(thing.ExecuteNonQuery() != 1, $"Moving-platform thing {record.ThingId} does not exist.");
        }
        UpdateThingPieces(record);
        transaction.Commit();
        return record with
        {
            DefinitionName = definition.Name,
            Width = definition.Width,
            Height = definition.Height,
            ColorArgb = definition.ColorArgb,
            Name = NormalizeOptionalName(record.Name)
        };
    }

    public MovingPlatformThingRecord2D DeleteMovingPlatform(long thingId)
    {
        var record = LoadMovingPlatforms().SingleOrDefault(item => item.ThingId == thingId);
        StateGuard.ThrowIf(record is null, $"Moving-platform thing {thingId} does not exist.");
        using var command = _connection.CreateCommand();
        command.CommandText = "DELETE FROM things WHERE thing_id = $thing_id;";
        command.Parameters.AddWithValue("$thing_id", thingId);
        StateGuard.ThrowIf(command.ExecuteNonQuery() != 1, $"Moving-platform thing {thingId} does not exist.");
        return record!;
    }

    public void RestoreMovingPlatform(MovingPlatformThingRecord2D record)
    {
        ArgGuard.ThrowIfNull(record);
        ValidateThing(record.X, record.Y, record.Rotation, record.TravelX, record.TravelY, record.Speed);
        using var transaction = _connection.BeginTransaction();
        RequireMovingPlatformDefinition(record.DefinitionId);
        using (var thing = _connection.CreateCommand())
        {
            thing.CommandText = """
                INSERT INTO things(thing_id, definition_id, name, enabled)
                VALUES($thing_id, $definition_id, $name, $enabled);
                """;
            thing.Parameters.AddWithValue("$thing_id", record.ThingId);
            thing.Parameters.AddWithValue("$definition_id", record.DefinitionId);
            thing.Parameters.AddWithValue("$name", (object?)NormalizeOptionalName(record.Name) ?? DBNull.Value);
            thing.Parameters.AddWithValue("$enabled", record.Enabled ? 1 : 0);
            thing.ExecuteNonQuery();
        }
        InsertThingPieces(record.ThingId, record.X, record.Y, record.Rotation, record.TravelX, record.TravelY, record.Speed);
        transaction.Commit();
    }

    private MovingPlatformDefinitionRecord2D RequireMovingPlatformDefinition(long definitionId) =>
        LoadMovingPlatformDefinitions().SingleOrDefault(item => item.DefinitionId == definitionId) ??
        throw new InvalidOperationException($"Moving-platform definition {definitionId} does not exist.");

    private ThingDefinitionRecord2D RequireThingDefinition(long definitionId) =>
        LoadThingDefinitions().SingleOrDefault(item => item.DefinitionId == definitionId) ??
        throw new InvalidOperationException($"Thing definition {definitionId} does not exist.");

    private static MovingPlatformThingRecord2D ResolveThing(
        long thingId,
        MovingPlatformDefinitionRecord2D definition,
        NewMovingPlatformThing2D input) =>
        new(
            thingId,
            definition.DefinitionId,
            definition.Name,
            NormalizeOptionalName(input.Name),
            input.Enabled,
            definition.Width,
            definition.Height,
            definition.ColorArgb,
            input.X,
            input.Y,
            input.Rotation,
            input.TravelX,
            input.TravelY,
            input.Speed);

    private void InsertDefinitionPieces(long definitionId, float width, float height, int colorArgb)
    {
        using (var shape = _connection.CreateCommand())
        {
            shape.CommandText = "INSERT INTO rectangle_shapes(definition_id, width, height) VALUES($id, $width, $height);";
            shape.Parameters.AddWithValue("$id", definitionId);
            shape.Parameters.AddWithValue("$width", width);
            shape.Parameters.AddWithValue("$height", height);
            shape.ExecuteNonQuery();
        }
        using var art = _connection.CreateCommand();
        art.CommandText = "INSERT INTO solid_color_art(definition_id, color_argb) VALUES($id, $color);";
        art.Parameters.AddWithValue("$id", definitionId);
        art.Parameters.AddWithValue("$color", colorArgb);
        art.ExecuteNonQuery();
    }

    private void InsertThingPieces(long thingId, float x, float y, float rotation, float travelX, float travelY, float speed)
    {
        using (var transform = _connection.CreateCommand())
        {
            transform.CommandText = "INSERT INTO transforms(thing_id, x, y, rotation) VALUES($id, $x, $y, $rotation);";
            transform.Parameters.AddWithValue("$id", thingId);
            transform.Parameters.AddWithValue("$x", x);
            transform.Parameters.AddWithValue("$y", y);
            transform.Parameters.AddWithValue("$rotation", rotation);
            transform.ExecuteNonQuery();
        }
        using var motor = _connection.CreateCommand();
        motor.CommandText = "INSERT INTO ping_pong_motors(thing_id, travel_x, travel_y, speed) VALUES($id, $travel_x, $travel_y, $speed);";
        motor.Parameters.AddWithValue("$id", thingId);
        motor.Parameters.AddWithValue("$travel_x", travelX);
        motor.Parameters.AddWithValue("$travel_y", travelY);
        motor.Parameters.AddWithValue("$speed", speed);
        motor.ExecuteNonQuery();
    }

    private void UpdateThingPieces(MovingPlatformThingRecord2D record)
    {
        using (var transform = _connection.CreateCommand())
        {
            transform.CommandText = "UPDATE transforms SET x = $x, y = $y, rotation = $rotation WHERE thing_id = $id;";
            transform.Parameters.AddWithValue("$x", record.X);
            transform.Parameters.AddWithValue("$y", record.Y);
            transform.Parameters.AddWithValue("$rotation", record.Rotation);
            transform.Parameters.AddWithValue("$id", record.ThingId);
            StateGuard.ThrowIf(transform.ExecuteNonQuery() != 1, $"Moving-platform thing {record.ThingId} is missing transforms.");
        }
        using var motor = _connection.CreateCommand();
        motor.CommandText = "UPDATE ping_pong_motors SET travel_x = $travel_x, travel_y = $travel_y, speed = $speed WHERE thing_id = $id;";
        motor.Parameters.AddWithValue("$travel_x", record.TravelX);
        motor.Parameters.AddWithValue("$travel_y", record.TravelY);
        motor.Parameters.AddWithValue("$speed", record.Speed);
        motor.Parameters.AddWithValue("$id", record.ThingId);
        StateGuard.ThrowIf(motor.ExecuteNonQuery() != 1, $"Moving-platform thing {record.ThingId} is missing ping_pong_motors.");
    }

    private static void ValidateDefinition(string name, float width, float height)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(name);
        ArgGuard.ThrowIfNotPositive(width);
        ArgGuard.ThrowIfNotPositive(height);
    }

    private static void ValidateThing(float x, float y, float rotation, float travelX, float travelY, float speed)
    {
        ArgGuard.ThrowIfNotFinite(x);
        ArgGuard.ThrowIfNotFinite(y);
        ArgGuard.ThrowIfNotFinite(rotation);
        ArgGuard.ThrowIfNotFinite(travelX);
        ArgGuard.ThrowIfNotFinite(travelY);
        ArgGuard.ThrowIfNotPositive(speed);
        StateGuard.ThrowIf(travelX == 0f && travelY == 0f, "A moving platform needs non-zero travel.");
    }

    private static void ValidateTransform(float x, float y, float rotation)
    {
        ArgGuard.ThrowIfNotFinite(x);
        ArgGuard.ThrowIfNotFinite(y);
        ArgGuard.ThrowIfNotFinite(rotation);
    }

    private static string? NormalizeOptionalName(string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    private static void RequirePiece(SqliteDataReader reader, int ordinal, long definitionId, string table) =>
        StateGuard.ThrowIf(
            reader.IsDBNull(ordinal),
            $"Thing definition {definitionId} of type '{MovingPlatformTypeKey}' is missing required piece '{table}'.");

    private static void RequireThingPiece(SqliteDataReader reader, int ordinal, long thingId, string table) =>
        RequireThingPiece(reader, ordinal, thingId, table, MovingPlatformTypeKey);

    private static void RequireThingPiece(SqliteDataReader reader, int ordinal, long thingId, string table, string typeKey) =>
        StateGuard.ThrowIf(
            reader.IsDBNull(ordinal),
            $"Thing {thingId} of type '{typeKey}' is missing required piece '{table}'.");
}
