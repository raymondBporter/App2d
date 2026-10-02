using App2d.Contracts.World;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Levels;

/// <summary>Durable zone authoring alongside level.db; no audio or simulation dependencies.</summary>
public static class WorldZoneFile2D
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static ImmutableArray<WorldZone2D> Load(string path)
    {
        if (!File.Exists(path)) return [];
        try
        {
            var file = JsonSerializer.Deserialize<ZoneFile>(File.ReadAllText(path), Options)
                ?? throw new InvalidDataException("Zone document is null.");
            if (file.Version != 1 || file.Zones is null) throw new InvalidDataException("Expected zone format version 1.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var zones = ImmutableArray.CreateBuilder<WorldZone2D>(file.Zones.Length);
            foreach (var entry in file.Zones)
            {
                if (entry is null) throw new InvalidDataException("A zone entry cannot be null.");
                var zone = new WorldZone2D(entry.Id, entry.Name,
                    new(new(entry.MinX, entry.MinY), new(entry.MaxX, entry.MaxY)), entry.Priority);
                if (!ids.Add(zone.Id)) throw new InvalidDataException($"Duplicate zone ID '{zone.Id}'.");
                zones.Add(zone);
            }
            return zones.MoveToImmutable();
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidDataException)
        {
            throw new InvalidDataException($"Invalid zones in '{path}': {e.Message}", e);
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Instantiated by System.Text.Json.")]
    private sealed record ZoneFile(int Version, ZoneEntry[] Zones);
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1812", Justification = "Instantiated by System.Text.Json.")]
    private sealed record ZoneEntry(string Id, string Name,
        [property: JsonRequired] float MinX, [property: JsonRequired] float MinY,
        [property: JsonRequired] float MaxX, [property: JsonRequired] float MaxY, int Priority);
}
