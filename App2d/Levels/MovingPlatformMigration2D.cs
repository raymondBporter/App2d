using App2d.Core.Characters.Authored;
using App2d.Core.Characters;
using App2d.Core.Geometry.Curves;
using App2d.Core.Mathematics.Progress;
using App2d.Core.Objects;
using App2d.Core.Physics;
using App2d.Core.Physics.Motion;
using App2d.Core.Shapes;
using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace App2d.Levels;

/// <summary>
/// Format version 3: the August moving-platform rows become object assets and object things. Each distinct
/// travel and speed of a definition becomes one asset file under the world root's <c>objects</c> folder; the
/// things keep their transforms and point at object definitions. Runs inside the migration transaction, writes
/// nothing until every target file is known to be free or identical, and drops the platform piece tables last.
/// </summary>
internal static class MovingPlatformMigration2D
{
    private const string LegacyTypeKey = "moving-platform";
    private const string Folder = "objects";
    private static readonly string[] PieceTables = ["ping_pong_motors", "solid_color_art", "rectangle_shapes"];

    public static void Convert(SqliteConnection connection, string worldRoot)
    {
        if (!TableExists(connection, "rectangle_shapes"))
        {
            return;
        }

        RequireCompletePlatforms(connection);
        var plan = new List<Group>();
        var unusedDefinitions = new List<long>();
        var usedSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var definition in ReadDefinitions(connection))
        {
            var groups = ReadThings(connection, definition.Id).GroupBy(t => (t.TravelX, t.TravelY, t.Speed)).Select(g => g.ToList()).ToList();
            if (groups.Count == 0)
            {
                unusedDefinitions.Add(definition.Id);
                continue;
            }

            for (var index = 0; index < groups.Count; index++)
            {
                var things = groups[index];
                var baseName = things.Count == 1 && !string.IsNullOrWhiteSpace(things[0].Name) ? things[0].Name!.Trim() : definition.Name;
                var (name, slug) = Unique(baseName, usedSlugs);
                var asset = BuildAsset(slug, name, definition, things[0]);
                plan.Add(new Group(definition.Id, index == 0, name, Path.Combine(worldRoot, Folder, slug + ".json"), Folder + "/" + slug + ".json", asset, [.. things.Select(t => t.Id)]));
            }
        }

        foreach (var group in plan)
        {
            EnsureFreeOrIdentical(group);
        }

        if (plan.Count > 0)
        {
            Directory.CreateDirectory(Path.Combine(worldRoot, Folder));
        }

        foreach (var group in plan)
        {
            if (!File.Exists(group.File))
            {
                group.Asset.Save(group.File);
            }
        }

        foreach (var group in plan)
        {
            Apply(connection, group);
        }

        foreach (var definitionId in unusedDefinitions)
        {
            Execute(connection, "DELETE FROM thing_definitions WHERE definition_id = $id;", ("$id", definitionId));
        }

        foreach (var table in PieceTables)
        {
            Execute(connection, $"DROP TABLE IF EXISTS {table};");
        }
    }

    /// <summary>The old editor always wrote every piece; a platform missing one is a damaged file, refused as it is rather than half-converted.</summary>
    private static void RequireCompletePlatforms(SqliteConnection connection)
    {
        using (var definitions = connection.CreateCommand())
        {
            definitions.CommandText = """
                SELECT d.definition_id, d.name
                FROM thing_definitions d
                LEFT JOIN rectangle_shapes r USING(definition_id)
                LEFT JOIN solid_color_art a USING(definition_id)
                WHERE d.type_key = $type AND (r.definition_id IS NULL OR a.definition_id IS NULL)
                ORDER BY d.definition_id;
                """;
            definitions.Parameters.AddWithValue("$type", LegacyTypeKey);
            using var reader = definitions.ExecuteReader();
            if (reader.Read())
            {
                throw new InvalidDataException($"Moving-platform definition {reader.GetInt64(0)} '{reader.GetString(1)}' is missing its rectangle or art row, so the level cannot be converted as it is.");
            }
        }

        using (var things = connection.CreateCommand())
        {
            things.CommandText = """
                SELECT t.thing_id, t.name, d.name
                FROM things t
                JOIN thing_definitions d USING(definition_id)
                LEFT JOIN ping_pong_motors m USING(thing_id)
                WHERE d.type_key = $type AND m.thing_id IS NULL
                ORDER BY t.thing_id;
                """;
            things.Parameters.AddWithValue("$type", LegacyTypeKey);
            using var reader = things.ExecuteReader();
            if (reader.Read())
            {
                var name = reader.IsDBNull(1) ? reader.GetString(2) : reader.GetString(1);
                throw new InvalidDataException($"Moving-platform thing {reader.GetInt64(0)} '{name}' is missing its motor row, so the level cannot be converted as it is.");
            }
        }
    }

    private static Part BuildAsset(string slug, string name, Definition definition, Thing sample)
    {
        var travel = new Vector2(sample.TravelX, sample.TravelY);
        var half = new Vector2(definition.Width / 2f, definition.Height / 2f);
        return new Part
        {
            Id = slug,
            Name = name,
            Geometry = [new() { Id = "shape", A = "origin", Frame = "origin", Geometry = Rectangle2D.Builder.Create(-half, half), Material = new RenderMaterialDefinition2D { Fill = "#" + (definition.ColorArgb & 0xFFFFFF).ToString("x6", CultureInfo.InvariantCulture) }, }],
            Physics = new PhysicsBodySettings2D { MotionType = BodyMotionType2D.Kinematic, Restitution = 0f, TransfersContactMotion = true },
            Surface = new ObjectSurface2D { OneWayNormal = Vector2.UnitY },
            Motion = new CurveMotionDefinition2D
            {
                Curve = LineSegmentCurve2D.Builder.Create(Vector2.Zero, travel),
                PeriodSeconds = 2 * travel.Length() / sample.Speed,
                Progress = [ProgressMapKind.PingPong, ProgressMapKind.Smooth]
            }
        };
    }

    private static void EnsureFreeOrIdentical(Group group)
    {
        if (!File.Exists(group.File))
        {
            return;
        }

        if (Normalize(File.ReadAllText(group.File)) != Normalize(group.Asset.ToJson()))
        {
            throw new InvalidDataException($"Migrating the moving platforms would overwrite '{group.File}' with a different object; move that file aside and open the level again.");
        }
    }

    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd();

    private static (string Name, string Slug) Unique(string baseName, HashSet<string> usedSlugs)
    {
        var baseSlug = Slugify(baseName);
        if (usedSlugs.Add(baseSlug))
        {
            return (baseName, baseSlug);
        }

        for (var suffix = 2; ; suffix++)
        {
            var slug = baseSlug + "-" + suffix.ToString(CultureInfo.InvariantCulture);
            if (usedSlugs.Add(slug))
            {
                return (baseName + "-" + suffix.ToString(CultureInfo.InvariantCulture), slug);
            }
        }
    }

    private static string Slugify(string name)
    {
        var builder = new StringBuilder(name.Length);
        var pendingDash = false;
        foreach (var character in name.Trim().ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                if (pendingDash && builder.Length > 0)
                {
                    builder.Append('-');
                }

                pendingDash = false;
                builder.Append(character);
            }
            else
            {
                pendingDash = true;
            }
        }

        return builder.Length == 0 ? "object" : builder.ToString();
    }

    private static void Apply(SqliteConnection connection, Group group)
    {
        long definitionId;
        if (group.KeepsDefinition)
        {
            definitionId = group.DefinitionId;
            Execute(connection, "UPDATE thing_definitions SET type_key = $type, name = $name WHERE definition_id = $id;", ("$type", LevelDatabase2D.ObjectTypeKey), ("$name", group.Name), ("$id", definitionId));
        }
        else
        {
            definitionId = Scalar(connection, "INSERT INTO thing_definitions(type_key, name) VALUES($type, $name) RETURNING definition_id;", ("$type", LevelDatabase2D.ObjectTypeKey), ("$name", group.Name));
            foreach (var thingId in group.ThingIds)
            {
                Execute(connection, "UPDATE things SET definition_id = $definition WHERE thing_id = $thing;", ("$definition", definitionId), ("$thing", thingId));
            }
        }

        Execute(connection, "INSERT INTO object_references(definition_id, asset) VALUES($id, $asset);", ("$id", definitionId), ("$asset", group.AssetPath));
    }

    private static List<Definition> ReadDefinitions(SqliteConnection connection)
    {
        var definitions = new List<Definition>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT d.definition_id, d.name, r.width, r.height, a.color_argb
            FROM thing_definitions d
            JOIN rectangle_shapes r USING(definition_id)
            JOIN solid_color_art a USING(definition_id)
            WHERE d.type_key = $type
            ORDER BY d.definition_id;
            """;
        command.Parameters.AddWithValue("$type", LegacyTypeKey);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            definitions.Add(new Definition(reader.GetInt64(0), reader.GetString(1), reader.GetFloat(2), reader.GetFloat(3), reader.GetInt32(4)));
        }

        return definitions;
    }

    private static List<Thing> ReadThings(SqliteConnection connection, long definitionId)
    {
        var things = new List<Thing>();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT t.thing_id, t.name, m.travel_x, m.travel_y, m.speed
            FROM things t
            JOIN ping_pong_motors m USING(thing_id)
            WHERE t.definition_id = $id
            ORDER BY t.thing_id;
            """;
        command.Parameters.AddWithValue("$id", definitionId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            things.Add(new Thing(reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetString(1), reader.GetFloat(2), reader.GetFloat(3), reader.GetFloat(4)));
        }

        return things;
    }

    private static bool TableExists(SqliteConnection connection, string table) =>
        Scalar(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = $name;", ("$name", table)) > 0;

    private static long Scalar(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return System.Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static void Execute(SqliteConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }

    private sealed record Definition(long Id, string Name, float Width, float Height, int ColorArgb);
    private sealed record Thing(long Id, string? Name, float TravelX, float TravelY, float Speed);
    private sealed record Group(long DefinitionId, bool KeepsDefinition, string Name, string File, string AssetPath, Part Asset, List<long> ThingIds);
}
