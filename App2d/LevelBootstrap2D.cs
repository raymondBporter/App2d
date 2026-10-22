using App2d.Core.Assets;
using App2d.Core.Geometry.Primitives;
using App2d.Core.IO;
using App2d.Core.Objects;
using App2d.Core.Validation;
using App2d.Levels;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace App2d;

/// <summary>
/// Resolves and loads the durable authored level file.
/// </summary>
internal static class LevelBootstrap2D
{
    private const string LevelId = "cavern";
    public static IReadOnlyList<string> TerrainTilesetIds { get; } =
    [
        "ink-medieval-ground",
        "ink-medieval-wall",
        "ink-prehistoric-ground",
        "ink-prehistoric-wall",
        "ink-wasteland-ground",
        "ink-wasteland-wall"
    ];

    /// <summary>
    /// Levels are durable authored content, so they live under <c>Assets/Static</c> and are
    /// read from there directly in Debug. <c>Assets/Runtime</c> is generated and disposable
    /// and must never be the only home for a hand-edited file.
    /// </summary>
    public static string CavernLevelPath { get; } = Path.Combine(AssetPaths.Current.Levels, LevelId, "level.db");
    public static string WorldManifestPath { get; } = Path.Combine(AssetPaths.Current.Levels, LevelId, "world.json");
    public static string GiantTreesPath { get; } = Path.Combine(AssetPaths.Current.Levels, LevelId, "giant-trees.json");

    public static WorldAsset2D Load() => LoadManifest(WorldManifestPath);

    public static WorldAsset2D LoadWorld(string manifest)
    {
        if (!File.Exists(manifest))
        {
            throw new FileNotFoundException("World manifest is missing.", manifest);
        }

        return LoadManifest(manifest);
    }

    /// <summary>
    /// Brings a world written before level format 3 up to date. Every map file under the manifest's folder is opened
    /// once so its database migrates (moving-platform rows become object assets under <c>objects/</c> and object
    /// things), and a manifest <c>objects</c> array moves into the map whose bounds contain each placement and is then
    /// dropped from the manifest. Running it again changes nothing. Returns every map file it opened, in ordinal order.
    /// </summary>
    public static IReadOnlyList<string> MigrateWorld(string manifestPath)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(manifestPath);
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("World manifest is missing.", manifestPath);
        }

        var manifest = Path.GetFullPath(manifestPath);
        var root = Path.GetDirectoryName(manifest)!;
        var databases = Directory.EnumerateFiles(root, "*.db", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
        foreach (var path in databases)
        {
            using var database = LevelDatabase2D.Open(path, root);
        }

        ImportManifestObjects(manifest, root);
        return databases;
    }

    private static void ImportManifestObjects(string manifest, string root)
    {
        var document = JsonNode.Parse(File.ReadAllText(manifest)) as JsonObject
            ?? throw new InvalidDataException($"Invalid world '{manifest}': the manifest is not a JSON object.");
        if (document["objects"] is not JsonArray entries)
        {
            return;
        }

        var maps = LoadManifestMaps(document, manifest, root);
        // Every entry is resolved before any row is written, so a bad entry leaves the maps and the manifest as they were.
        var imports = new List<(string MapPath, string Id, string Asset, string Name, Vector2 Point)>();
        foreach (var entry in entries)
        {
            var id = entry?["id"]?.GetValue<string>();
            var asset = entry?["asset"]?.GetValue<string>();
            var position = entry?["position"];
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(asset) || position?["x"] is null || position["y"] is null)
            {
                throw new InvalidDataException($"Invalid world '{manifest}': every manifest object needs an id, an asset and a position.");
            }

            var point = new Vector2(position["x"]!.GetValue<float>(), position["y"]!.GetValue<float>());
            var map = maps.FirstOrDefault(m => Contains(m.TileMap.WorldBounds, point))
                ?? throw new InvalidDataException($"Invalid world '{manifest}': no map contains object '{id}' at ({point.X}, {point.Y}).");
            ObjectAsset2D loaded;
            try
            {
                loaded = ObjectAsset2D.Load(FilePaths.ResolveUnderRoot(root, asset));
            }
            catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException or JsonException)
            {
                throw new InvalidDataException($"Invalid world '{manifest}': object '{id}' names asset '{asset}', which cannot be loaded: {e.Message}", e);
            }

            // The definition is named like the inspector names a new one: after the asset, falling back to its file.
            var name = string.IsNullOrWhiteSpace(loaded.Name) ? Path.GetFileNameWithoutExtension(asset) : loaded.Name.Trim();
            imports.Add((map.DatabasePath, id, asset, name, point));
        }

        foreach (var group in imports.GroupBy(import => import.MapPath, StringComparer.OrdinalIgnoreCase))
        {
            using var database = LevelDatabase2D.Open(group.Key, root);
            foreach (var (_, id, asset, name, point) in group)
            {
                var definition = database.LoadThingDefinitions().FirstOrDefault(d =>
                        string.Equals(d.TypeKey, LevelDatabase2D.ObjectTypeKey, StringComparison.Ordinal) &&
                        string.Equals(d.Asset, asset, StringComparison.Ordinal))
                    ?? database.CreateThingDefinition(LevelDatabase2D.ObjectTypeKey, name, asset);
                database.CreatePositionThing(new NewPositionThing2D(definition.DefinitionId, id, true, point.X, point.Y));
            }
        }

        document.Remove("objects");
        AtomicFile.WriteAllText(manifest, document.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine);
    }

    private static List<WorldMapAsset2D> LoadManifestMaps(JsonObject document, string manifest, string root)
    {
        if (document["zones"] is not JsonArray zones)
        {
            throw new InvalidDataException($"Invalid world '{manifest}': expected a zones array.");
        }

        var maps = new List<WorldMapAsset2D>();
        foreach (var zone in zones)
        {
            if (zone?["maps"] is not JsonArray zoneMaps)
            {
                continue;
            }

            foreach (var map in zoneMaps)
            {
                var file = map?["file"]?.GetValue<string>();
                if (map is null || string.IsNullOrWhiteSpace(file) || Path.IsPathRooted(file))
                {
                    throw new InvalidDataException($"Invalid world '{manifest}': maps need relative file paths.");
                }

                var path = Path.GetFullPath(Path.Combine(root, file));
                if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"Invalid world '{manifest}': map file '{file}' is outside the world directory.");
                }

                maps.Add(WorldFile2D.LoadMap(path,
                    map["id"]?.GetValue<string>() ?? file,
                    map["name"]?.GetValue<string>() ?? file,
                    zone["id"]?.GetValue<string>() ?? "",
                    map["identity"]?.GetValue<int>() ?? 0,
                    TerrainTilesetIds,
                    root));
            }
        }

        return maps;
    }

    private static bool Contains(Rect2D bounds, Vector2 point) =>
        point.X >= bounds.Min.X && point.X <= bounds.Max.X && point.Y >= bounds.Min.Y && point.Y <= bounds.Max.Y;

    private static WorldAsset2D LoadManifest(string manifest)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(manifest))!;
        if (File.Exists(manifest))
        {
            using var world = WorldFile2D.Load(manifest, TerrainTilesetIds);
            // Existing biome/audio regions remain available inside authored map containers.
            var regionIds = world.Zones.Select(z => z.Id).ToHashSet(StringComparer.Ordinal);
            var regions = WorldZoneFile2D.Load(Path.Combine(directory, "zones.json"));
            return new(world.Maps, [.. world.Zones, .. regions.Where(z => !regionIds.Contains(z.Id))])
            {
                Root = world.Root,
                StartAtArrival = world.StartAtArrival
            };
        }
        return new WorldAsset2D(
            [WorldFile2D.LoadMap(RequireLevelPath(), "cavern", "Cavern", "cavern", 0, TerrainTilesetIds)],
            WorldZoneFile2D.Load(Path.Combine(directory, "zones.json")))
        { Root = directory };
    }
    /// <summary>
    /// Opens the cavern level read-write for an editing session. The caller owns the
    /// returned database and must dispose it.
    /// </summary>
    public static LevelDatabase2D OpenForEditing() => LevelDatabase2D.Open(RequireLevelPath());

    private static string RequireLevelPath() =>
        File.Exists(CavernLevelPath)
            ? CavernLevelPath
            : throw new FileNotFoundException(
                "The authored cavern level is missing. Restore Assets/Static/levels/cavern/level.db.",
                CavernLevelPath);
}
