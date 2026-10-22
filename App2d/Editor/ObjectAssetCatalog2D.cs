using App2d.Core.IO;
using App2d.Core.Objects;
using App2d.Core.Validation;

namespace App2d.Editor;

/// <summary>The object asset files under a world's <c>objects</c> folder, by path relative to the world root, each loaded once.</summary>
internal sealed class ObjectAssetCatalog2D
{
    private const string Folder = "objects";
    private readonly string _worldRoot;
    private readonly Dictionary<string, ObjectAsset2D> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> _paths = [];

    public ObjectAssetCatalog2D(string worldRoot)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(worldRoot);
        _worldRoot = Path.GetFullPath(worldRoot);
        Refresh();
    }

    /// <summary>Relative paths such as <c>objects/lift.json</c>, forward slashes, in ordinal order.</summary>
    public IReadOnlyList<string> Paths => _paths;

    public ObjectAsset2D Load(string relativePath)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(relativePath);
        var resolved = FilePaths.ResolveUnderRoot(_worldRoot, relativePath);
        if (!_loaded.TryGetValue(resolved, out var asset))
        {
            _loaded.Add(resolved, asset = ObjectAsset2D.Load(resolved));
        }

        return asset;
    }

    /// <summary>Rescans the folder; assets already loaded stay cached.</summary>
    public void Refresh()
    {
        var folder = Path.Combine(_worldRoot, Folder);
        _paths = Directory.Exists(folder)
            ? [.. Directory.EnumerateFiles(folder, "*.json").Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).Select(f => Folder + "/" + Path.GetFileName(f)).Order(StringComparer.Ordinal)]
            : [];
    }
}
