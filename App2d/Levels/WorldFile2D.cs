using App2d.Contracts.World;
using App2d.Core.Geometry.Primitives;
using App2d.Core.IO;
using App2d.Core.Objects;
using App2d.Core.Tiles;
using App2d.Gameplay.World.Vegetation;
using App2d.Things;
using System.Collections.Immutable;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Levels;

/// <summary>A map's durable identity and independently editable database.</summary>
public sealed class WorldMapAsset2D
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string ZoneId { get; init; }
    public required string DatabasePath { get; init; }
    public required int Identity { get; init; }
    public required EditableTileMap2D TileMap { get; init; }
    public required IReadOnlyList<MovingPlatformThingRecord2D> MovingPlatforms { get; set; }
    public required IReadOnlyList<PositionThingRecord2D> PositionThings { get; init; }
    /// <summary>The map's object things as placements, ids already world-wide.</summary>
    public required IReadOnlyList<ObjectPlacement2D> Objects { get; set; }

    // Stable across manifest reordering; namespace zero preserves existing save-point IDs.
    public long WorldThingId(long localId)
    {
        if (localId <= 0 || localId > uint.MaxValue)
        {
            throw new InvalidDataException($"Map '{Id}' has a thing ID outside the supported positive 32-bit range.");
        }

        return ((long)Identity << 32) | localId;
    }
}

public sealed class WorldAsset2D : IDisposable
{
    public WorldAsset2D(IReadOnlyList<WorldMapAsset2D> maps, ImmutableArray<WorldZone2D> zones)
    {
        Maps = maps;
        Zones = zones;
        TileMap = new WorldTileMap2D(maps.Select(m => m.TileMap));
    }

    public IReadOnlyList<WorldMapAsset2D> Maps { get; }

    public ImmutableArray<WorldZone2D> Zones { get; }

    public bool StartAtArrival { get; init; }
    /// <summary>The folder holding <c>world.json</c>; object asset paths resolve under it.</summary>
    public required string Root { get; init; }
    /// <summary>Placements the manifest itself lists; superseded by map databases.</summary>
    public IReadOnlyList<ObjectPlacement2D> ManifestObjects { get; init; } = [];
    public IReadOnlyList<ObjectPlacement2D> Objects => [.. ManifestObjects, .. Maps.SelectMany(m => m.Objects)];

    public ImmutableArray<GiantTree2D> GiantTrees => LoadGiantTrees();

    public ImmutableArray<GiantTree2D> LoadGiantTrees() =>
    [
        .. Maps
        .Where(m => Path.GetFileName(m.DatabasePath) == "level.db")
        .SelectMany(m => GiantTreeFile2D.Load(Path.Combine(Path.GetDirectoryName(m.DatabasePath)!, "giant-trees.json"), m.TileMap))
    ];

    public WorldTileMap2D TileMap { get; }
    public IReadOnlyList<MovingPlatformThingRecord2D> MovingPlatforms => [.. Maps.SelectMany(m => m.MovingPlatforms.Select(t => t with { ThingId = m.WorldThingId(t.ThingId) }))];
    public IReadOnlyList<PositionThingRecord2D> PositionThings => [.. Maps.SelectMany(m => m.PositionThings.Select(t => t with { ThingId = m.WorldThingId(t.ThingId) }))];
    public void Dispose() => TileMap.Dispose();
}

/// <summary>World manifest: zones own map files; each database stores world-space tile and thing positions.</summary>
public static class WorldFile2D
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new Vector2JsonConverter() },
        WriteIndented = true
    };

    public static WorldAsset2D Load(string path, IReadOnlyList<string>? legacyTilesets = null)
    {
        try
        {
            var file = JsonSerializer.Deserialize<WorldDocument>(File.ReadAllText(path), Options)
                ?? throw new InvalidDataException("World document is null.");
            if (file.Version != 1 || file.Zones is null || file.Zones.Count == 0)
            {
                throw new InvalidDataException("Expected world version 1 and at least one zone.");
            }

            var root = Path.GetDirectoryName(Path.GetFullPath(path))!;
            var objects = LoadObjects(root, file.Objects);
            var zoneIds = new HashSet<string>(StringComparer.Ordinal);
            var mapIds = new HashSet<string>(StringComparer.Ordinal);
            var identities = new HashSet<int>();
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var maps = new List<WorldMapAsset2D>();
            var zones = ImmutableArray.CreateBuilder<WorldZone2D>();
            foreach (var zone in file.Zones)
            {
                if (zone is null || string.IsNullOrWhiteSpace(zone.Id) || string.IsNullOrWhiteSpace(zone.Name) ||
                    !zoneIds.Add(zone.Id) || zone.Maps is null || zone.Maps.Count == 0)
                {
                    throw new InvalidDataException("Zones need unique IDs, names, and at least one map.");
                }

                var min = new Vector2(float.PositiveInfinity);
                var max = new Vector2(float.NegativeInfinity);
                foreach (var map in zone.Maps)
                {
                    if (map is null || string.IsNullOrWhiteSpace(map.Id) || string.IsNullOrWhiteSpace(map.Name) ||
                        string.IsNullOrWhiteSpace(map.File) || !mapIds.Add(map.Id) || map.Identity < 0 || !identities.Add(map.Identity))
                    {
                        throw new InvalidDataException("Maps need unique IDs, nonnegative identities, names and file paths.");
                    }

                    var databasePath = Path.GetFullPath(Path.Combine(root, map.File));
                    if (Path.IsPathRooted(map.File) || !databasePath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                        !paths.Add(databasePath))
                    {
                        throw new InvalidDataException("Map files must be unique relative paths within the world directory.");
                    }

                    var loaded = LoadMap(databasePath, map.Id, map.Name, zone.Id, map.Identity, legacyTilesets, root);
                    maps.Add(loaded);
                    min = Vector2.Min(min, loaded.TileMap.WorldBounds.Min);
                    max = Vector2.Max(max, loaded.TileMap.WorldBounds.Max);
                }
                zones.Add(new(zone.Id, zone.Name, new Rect2D(min, max), zone.Priority));
            }
            if (file.StartMapId is not null)
            {
                var index = maps.FindIndex(m => m.Id == file.StartMapId);
                if (index < 0)
                {
                    throw new InvalidDataException($"Start map '{file.StartMapId}' does not exist.");
                }

                var start = maps[index];
                maps.RemoveAt(index);
                maps.Insert(0, start);
            }
            return new(maps.AsReadOnly(), zones.ToImmutable())
            {
                Root = root,
                ManifestObjects = objects,
                StartAtArrival = file.StartAtArrival
            };
        }
        catch (Exception e) when (e is JsonException or ArgumentException or OverflowException)
        {
            throw new InvalidDataException($"Invalid world '{path}': {e.Message}", e);
        }
    }

    /// <summary><paramref name="worldRoot"/> is where object asset paths resolve; the map's own directory when it has no world file.</summary>
    public static WorldMapAsset2D LoadMap(string path, string id, string name, string zoneId, int identity, IReadOnlyList<string>? legacyTilesets = null, string? worldRoot = null)
    {
        var root = worldRoot ?? Path.GetDirectoryName(Path.GetFullPath(path))!;
        using var database = LevelDatabase2D.OpenRead(path);
        var result = new WorldMapAsset2D
        {
            Id = id,
            Name = name,
            ZoneId = zoneId,
            Identity = identity,
            DatabasePath = path,
            TileMap = database.Load(legacyTilesets),
            MovingPlatforms = database.LoadMovingPlatforms(),
            PositionThings = database.LoadPositionThings(),
            Objects = []
        };
        foreach (var thing in result.PositionThings)
        {
            _ = result.WorldThingId(thing.ThingId);
        }

        result.Objects = LoadMapObjects(root, result);
        return result;
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<ObjectPlacement2D> LoadMapObjects(string root, WorldMapAsset2D map)
    {
        var result = new List<ObjectPlacement2D>();
        var assets = new Dictionary<string, ObjectAsset2D>(StringComparer.OrdinalIgnoreCase);
        foreach (var thing in map.PositionThings)
        {
            if (!string.Equals(thing.TypeKey, LevelDatabase2D.ObjectTypeKey, StringComparison.Ordinal)) continue;
            ObjectAsset2D? asset;
            try
            {
                var assetPath = FilePaths.ResolveUnderRoot(root, thing.Asset ?? "");
                if (!assets.TryGetValue(assetPath, out asset)) assets.Add(assetPath, asset = ObjectAsset2D.Load(assetPath));
            }
            catch (Exception e) when (e is ArgumentException or IOException or UnauthorizedAccessException or JsonException)
            {
                throw new InvalidDataException($"Object definition '{thing.DefinitionName}' in map '{map.Id}' names asset '{thing.Asset}', which cannot be loaded: {e.Message}", e);
            }
            result.Add(ThingTypeRegistry2D.ToPlacement(thing, asset, map.WorldThingId(thing.ThingId)));
        }
        return result.AsReadOnly();
    }

    private static System.Collections.ObjectModel.ReadOnlyCollection<ObjectPlacement2D> LoadObjects(string root, IReadOnlyList<ObjectPlacementDocument> placements)
    {
        if (placements is null) throw new InvalidDataException("Object placements cannot be null.");
        var result = new List<ObjectPlacement2D>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var assets = new Dictionary<string, ObjectAsset2D>(StringComparer.OrdinalIgnoreCase);
        foreach (var placement in placements)
        {
            if (placement is null || string.IsNullOrWhiteSpace(placement.Id) || !ids.Add(placement.Id))
                throw new InvalidDataException("Object placements need unique IDs.");
            var assetPath = FilePaths.ResolveUnderRoot(root, placement.Asset);
            if (!assets.TryGetValue(assetPath, out var asset))
                assets.Add(assetPath, asset = ObjectAsset2D.Load(assetPath));
            var instance = new ObjectPlacement2D(placement.Id, asset, placement.Position);
            instance.Validate();
            result.Add(instance);
        }
        return result.AsReadOnly();
    }

    public sealed record WorldDocument(int Version, IReadOnlyList<ZoneDocument> Zones)
    {
        public IReadOnlyList<ObjectPlacementDocument> Objects { get; init; } = [];
        public string? StartMapId { get; init; }
        public bool StartAtArrival { get; init; }
    }
    public sealed record ObjectPlacementDocument(string Id, string Asset,
        [property: JsonConverter(typeof(Vector2JsonConverter))] Vector2 Position);
    public sealed record ZoneDocument(string Id, string Name, int Priority, IReadOnlyList<MapDocument> Maps);

    public sealed record MapDocument(string Id, string Name, [property: JsonRequired] int Identity, string File)
    {
        public ZoneGeneration2D? Generation { get; init; }
    }

    public sealed record ZoneGeneration2D(float OriginX, float OriginY, CombatLevelSettings2D Settings);
}
