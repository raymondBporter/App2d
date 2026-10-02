using App2d.Contracts.World;
using App2d.Audio;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App2d.Presentation.Audio;

public readonly record struct MusicSelection2D(string Piece, string Mood);

/// <summary>Presentation policy binds reusable world IDs to music, without adding audio to zone geometry.</summary>
public sealed class WorldSoundtrack2D
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    public ImmutableDictionary<string, MusicCue2D> Cues { get; }
    public MusicSelection2D Default { get; }
    public ImmutableDictionary<string, MusicSelection2D> Zones { get; }

    public WorldSoundtrack2D(IReadOnlyDictionary<string, MusicCue2D> cues, MusicSelection2D fallback,
        IReadOnlyDictionary<string, MusicSelection2D> zones)
    {
        Cues = cues.ToImmutableDictionary(StringComparer.Ordinal);
        Default = fallback;
        Zones = zones.ToImmutableDictionary(StringComparer.Ordinal);
        Validate(Default);
        foreach (var selection in Zones.Values) Validate(selection);
    }

    public MusicSelection2D ForZone(WorldZone2D? zone) => zone is not null && Zones.TryGetValue(zone.Id, out var selection) ? selection : Default;

    public void Validate(MusicSelection2D selection)
    {
        if (string.IsNullOrWhiteSpace(selection.Piece) || string.IsNullOrWhiteSpace(selection.Mood) ||
            !Cues.TryGetValue(selection.Piece, out var cue) || !cue.Moods.ContainsKey(selection.Mood))
            throw new InvalidDataException($"Unknown music selection: {selection}");
    }

    public static WorldSoundtrack2D Load(string root, IEnumerable<WorldZone2D> worldZones)
    {
        var file = JsonSerializer.Deserialize<SoundtrackFile>(File.ReadAllText(Path.Combine(root, "soundtrack.json")), Options)
            ?? throw new InvalidDataException("Empty soundtrack definition.");
        if (file.Version != 1 || file.Cues is not { Length: > 0 } || file.Zones is null)
            throw new InvalidDataException("Expected soundtrack format version 1.");
        var cues = new Dictionary<string, MusicCue2D>(StringComparer.Ordinal);
        foreach (var id in file.Cues)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
                throw new InvalidDataException("Music cue IDs must use letters, digits and hyphens.");
            if (!cues.TryAdd(id, MusicCue2D.Load(Path.Combine(root, id, "manifest.json"))))
                throw new InvalidDataException($"Duplicate music cue '{id}'.");
        }
        var ids = worldZones.Select(z => z.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var zoneId in file.Zones.Keys)
            if (!ids.Contains(zoneId)) throw new InvalidDataException($"Soundtrack refers to missing world zone '{zoneId}'.");
        return new(cues, file.Default, file.Zones);
    }

    private sealed record SoundtrackFile(int Version, string[] Cues, MusicSelection2D Default, Dictionary<string, MusicSelection2D> Zones);
}
