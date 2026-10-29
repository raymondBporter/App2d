using App2d.Core.Characters.Authored;
using App2d.Core.IO;
using App2d.Core.Validation;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace App2d.Editor;

/// <summary>The object asset files under a world's <c>objects</c> folder, by path relative to the world root, each loaded once.</summary>
internal sealed class ObjectAssetCatalog2D
{
    private const string Folder = "objects";
    private readonly string _worldRoot;
    private readonly Dictionary<string, Part> _loaded = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Exception> _failures = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<string> _paths = [];

    public ObjectAssetCatalog2D(string worldRoot)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(worldRoot);
        _worldRoot = Path.GetFullPath(worldRoot);
        Refresh();
    }

    /// <summary>Relative paths such as <c>objects/lift.json</c>, forward slashes, in ordinal order.</summary>
    public IReadOnlyList<string> Paths => _paths;

    /// <summary>A file that fails to load is remembered as failed until <see cref="Refresh"/>: the editor asks every frame.</summary>
    public Part Load(string relativePath)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(relativePath);
        var resolved = FilePaths.ResolveUnderRoot(_worldRoot, relativePath);
        if (_loaded.TryGetValue(resolved, out var asset))
        {
            return asset;
        }

        if (_failures.TryGetValue(resolved, out var failure))
        {
            ExceptionDispatchInfo.Throw(failure);
        }

        try
        {
            asset = Part.Load(resolved);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or JsonException)
        {
            _failures[resolved] = e;
            throw;
        }

        _loaded.Add(resolved, asset);
        return asset;
    }

    public bool TryLoad(string relativePath, [NotNullWhen(true)] out Part? asset)
    {
        try
        {
            asset = Load(relativePath);
            return true;
        }
        catch (Exception e) when (e is ArgumentException or IOException or InvalidDataException or JsonException)
        {
            asset = null;
            return false;
        }
    }

    /// <summary>Rescans the folder and forgets failed loads; assets already loaded stay cached.</summary>
    public void Refresh()
    {
        _failures.Clear();
        var folder = Path.Combine(_worldRoot, Folder);
        _paths = Directory.Exists(folder)
            ? [.. Directory.EnumerateFiles(folder, "*.json").Where(f => f.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).Select(f => Folder + "/" + Path.GetFileName(f)).Order(StringComparer.Ordinal)]
            : [];
    }
}
