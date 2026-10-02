using System.Collections.Immutable;
using System.Text.Json;

namespace App2d.Audio;

/// <summary>Validated authoring manifest for stereo stems sharing one musical timeline.</summary>
public sealed class MusicCue2D
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    public const int SampleRate = 44_100;
    public string Title { get; }
    public long LoopFrames { get; }
    public int BarFrames { get; }
    public ImmutableArray<string> StemPaths { get; }
    public ImmutableDictionary<string, ImmutableArray<float>> Moods { get; }

    internal MusicCue2D(string title, long loopFrames, int barFrames, ImmutableArray<string> paths,
        ImmutableDictionary<string, ImmutableArray<float>> moods)
    {
        Title = title; LoopFrames = loopFrames; BarFrames = barFrames; StemPaths = paths; Moods = moods;
    }

    public static MusicCue2D Load(string manifestPath)
    {
        var data = JsonSerializer.Deserialize<Manifest>(File.ReadAllText(manifestPath), Options)
            ?? throw new InvalidDataException($"Empty music manifest: {manifestPath}");
        if (string.IsNullOrWhiteSpace(data.Title) || data.SampleRate != SampleRate || data.Channels != 2 ||
            data.LoopStartFrame != 0 || data.LoopEndFrame <= 0 || !double.IsFinite(data.Bpm) ||
            data.Bpm < 20 || data.Bpm > 400 || data.BeatsPerBar is < 1 or > 16 ||
            data.Stems is not { Length: > 0 and <= 8 } || data.Moods is not { Count: > 0 })
            throw new InvalidDataException($"Invalid stereo music manifest: {manifestPath}");
        var bar = SampleRate * 60d / data.Bpm * data.BeatsPerBar;
        var barFrames = checked((int)Math.Round(bar));
        if (Math.Abs(bar - barFrames) > .0001 || data.LoopEndFrame % barFrames != 0)
            throw new InvalidDataException("Music bars and loops must align to whole sample frames.");
        var directory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var paths = ImmutableArray.CreateBuilder<string>(data.Stems.Length);
        foreach (var stem in data.Stems)
        {
            if (stem is null || string.IsNullOrWhiteSpace(stem.Id) || !ids.Add(stem.Id) ||
                string.IsNullOrWhiteSpace(stem.File) || Path.GetFileName(stem.File) != stem.File ||
                stem.File.IndexOfAny(['/', '\\', ':']) >= 0 || !stem.File.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Music stems need unique IDs and local Ogg filenames.");
            var path = Path.Combine(directory, stem.File);
            if (!File.Exists(path)) throw new FileNotFoundException("Music stem is missing.", path);
            paths.Add(path);
        }
        var moods = ImmutableDictionary.CreateBuilder<string, ImmutableArray<float>>(StringComparer.Ordinal);
        foreach (var (name, gains) in data.Moods)
        {
            if (string.IsNullOrWhiteSpace(name) || gains is null || gains.Length != paths.Count ||
                gains.Any(g => !float.IsFinite(g) || g is < 0 or > 1))
                throw new InvalidDataException($"Invalid music mood '{name}'.");
            moods.Add(name, [.. gains]);
        }
        return new(data.Title, data.LoopEndFrame, barFrames, paths.MoveToImmutable(), moods.ToImmutable());
    }

    private sealed record Manifest(string Title, int SampleRate, int Channels, long LoopStartFrame,
        long LoopEndFrame, double Bpm, int BeatsPerBar, Stem[] Stems, Dictionary<string, float[]> Moods);
    private sealed record Stem(string Id, string File);
}
