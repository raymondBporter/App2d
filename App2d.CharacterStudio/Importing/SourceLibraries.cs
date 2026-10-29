using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace App2d.CharacterStudio.Importing;

/// <summary>The imported point libraries listed in the characters catalog, loaded on first use and kept read-only.</summary>
public sealed class SourceLibraries
{
    public sealed record Entry(string Id, string Label, string Anatomy, string Path, int ClipCount);
    private readonly Dictionary<string, PointLibrary> _loaded = new(StringComparer.Ordinal);

    private SourceLibraries(string root, IReadOnlyList<Entry> entries)
    {
        Root = root;
        Entries = entries;
    }
    public string Root { get; }
    public IReadOnlyList<Entry> Entries { get; }

    /// <summary>Reads <c>catalog.json</c> under the characters root. A missing catalog means no sources, not an error.</summary>
    public static SourceLibraries Open(string charactersRoot)
    {
        var path = Path.Combine(charactersRoot, "catalog.json");
        if (!File.Exists(path))
        {
            return new(charactersRoot, []);
        }

        using var catalog = JsonDocument.Parse(File.ReadAllText(path));
        var entries = catalog.RootElement.GetProperty("libraries").EnumerateArray().Select(e => new Entry(
            e.GetProperty("id").GetString()!, e.GetProperty("label").GetString()!, e.GetProperty("anatomy").GetString()!,
            e.GetProperty("path").GetString()!, e.TryGetProperty("clipCount", out var count) ? count.GetInt32() : 0)).ToList();
        return new(charactersRoot, entries);
    }

    public bool TryGet(string id, [NotNullWhen(true)] out PointLibrary? library)
    {
        if (_loaded.TryGetValue(id, out library))
        {
            return true;
        }

        var entry = Entries.FirstOrDefault(e => e.Id == id);
        if (entry is null)
        {
            return false;
        }

        _loaded[id] = library = PointLibrary.Load(Path.Combine(Root, entry.Path));
        return true;
    }

    public PointLibrary Get(string id) => TryGet(id, out var library) ? library : throw new KeyNotFoundException($"No imported library '{id}'.");
}
