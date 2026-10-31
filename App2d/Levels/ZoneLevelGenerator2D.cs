using App2d.Core.IO;
using App2d.Core.Json;
using System.Text.Json;

namespace App2d.Levels;

/// <summary>Generates a new map revision, validates the world layout, then assigns it to its zone.</summary>
public static class ZoneLevelGenerator2D
{

    public static string Regenerate(string manifest, string zoneId, IReadOnlyList<string> tilesets)
    {
        var world = JsonSerializer.Deserialize<WorldFile2D.WorldDocument>(File.ReadAllText(manifest), JsonProfiles.World)
            ?? throw new InvalidDataException("World layout is null.");
        var zone = world.Zones.SingleOrDefault(z => z.Id == zoneId)
            ?? throw new ArgumentException($"Unknown zone '{zoneId}'.", nameof(zoneId));
        if (zone.Maps.Count != 1 || zone.Maps[0].Generation is not { } generation)
        {
            throw new InvalidDataException("Generation currently requires one map with generation settings in the selected zone.");
        }

        var map = zone.Maps[0];
        if (generation.Settings is null)
        {
            throw new InvalidDataException("Zone generation settings are missing.");
        }
        // Use the numeric identity for the folder; IDs are labels, never filesystem paths.
        var relative = $"generated/map-{map.Identity}/seed-{generation.Settings.Seed}-{Guid.NewGuid():N}";
        var root = Path.GetDirectoryName(Path.GetFullPath(manifest))!;
        var folder = Path.Combine(root, relative);
        CombatLevelGenerator2D.Create(folder, generation.Settings, tilesets, new(generation.OriginX, generation.OriginY));
        var updatedMap = map with
        {
            File = relative + "/level.db"
        };
        var updated = world with
        {
            Zones = [.. world.Zones.Select(z => z.Id == zoneId ? z with { Maps = [updatedMap] } : z)]
        };
        var json = JsonSerializer.Serialize(updated, JsonProfiles.World);
        var candidate = Path.Combine(root, $".world-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(candidate, json);
            // Includes map identity, grid alignment, overlap, and arrival-map validation.
            using var validated = WorldFile2D.Load(candidate);
            AtomicFile.WriteAllText(manifest, json);
        }
        finally
        {
            File.Delete(candidate);
        }
        return Path.Combine(folder, "level.db");
    }
}
