using System.Text.Json;

namespace App2d.Core.Characters;

/// <summary>Every authored character asset under one root, found by scanning its folders. IDs come from file contents; errors are collected per file, not thrown.</summary>
public sealed class AuthoredCatalog
{
    private readonly Dictionary<string, CharacterModel> _models = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ModelVariant> _variants = new(StringComparer.Ordinal);
    private readonly Dictionary<string, MotionClip> _animations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _paths = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ResolvedModel> _resolved = new(StringComparer.Ordinal);

    public IReadOnlyDictionary<string, CharacterModel> Models => _models;
    public IReadOnlyDictionary<string, ModelVariant> Variants => _variants;
    public IReadOnlyDictionary<string, MotionClip> Animations => _animations;
    public List<string> Errors { get; } = [];

    public static AuthoredCatalog Load(string root)
    {
        var catalog = new AuthoredCatalog();
        catalog.Scan(root, "models", CharacterModel.FromJson, catalog._models, m => m.Id);
        catalog.Scan(root, "variants", ModelVariant.FromJson, catalog._variants, v => v.Id);
        catalog.Scan(root, "animations", MotionClip.FromJson, catalog._animations, a => a.Id);
        foreach (var id in catalog._variants.Keys) catalog.Check(id, () => catalog.Resolve(id));
        foreach (var clip in catalog._animations.Values)
            catalog.Check(clip.Id, () =>
            {
                if (!catalog._models.ContainsKey(clip.Model)) throw new InvalidDataException($"Clip '{clip.Id}' references missing model '{clip.Model}'.");
                clip.Validate(catalog.Resolve(clip.Model));
            });
        return catalog;
    }

    /// <summary>Resolves a base model or variant by ID. Compiled once and shared.</summary>
    public ResolvedModel Resolve(string id)
    {
        if (_resolved.TryGetValue(id, out var cached)) return cached;
        ResolvedModel resolved;
        if (_models.TryGetValue(id, out var model)) resolved = ResolvedModel.From(model);
        else if (!_variants.TryGetValue(id, out var variant)) throw new KeyNotFoundException($"No model or variant '{id}'.");
        else if (!_models.TryGetValue(variant.Base, out model)) throw new InvalidDataException($"Variant '{id}' references missing base model '{variant.Base}'.");
        else resolved = ResolvedModel.From(model, variant);
        return _resolved[id] = resolved;
    }

    private void Scan<T>(string root, string folder, Func<string, T> parse, Dictionary<string, T> into, Func<T, string> id)
    {
        var directory = Path.Combine(root, folder);
        if (!Directory.Exists(directory)) return;
        foreach (var path in Directory.EnumerateFiles(directory, "*.json").Order(StringComparer.Ordinal))
        {
            try
            {
                var asset = parse(File.ReadAllText(path)); var key = id(asset);
                if (_paths.TryGetValue(key, out var other)) { Errors.Add($"{path}: id '{key}' is already used by {other}."); continue; }
                _paths[key] = path; into[key] = asset;
            }
            catch (Exception ex) when (IsFileError(ex)) { Errors.Add($"{path}: {ex.Message}"); }
        }
    }

    private void Check(string id, Action action)
    {
        try { action(); }
        catch (Exception ex) when (IsFileError(ex)) { Errors.Add($"{_paths[id]}: {ex.Message}"); }
    }

    /// <summary>Anything a malformed or unreadable file can raise. One bad file is reported against its path, never fatal to the scan.</summary>
    private static bool IsFileError(Exception ex) =>
        ex is InvalidDataException or JsonException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or KeyNotFoundException;
}
