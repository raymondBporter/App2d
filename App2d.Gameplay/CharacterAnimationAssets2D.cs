using System.Text.Json;
using App2d.Core;
using App2d.Core.Animation;
using App2d.Rendering.Textures;

namespace App2d.Gameplay;

internal static class CharacterAnimationAssets2D
{
    private static readonly Lock ManifestLock = new();
    private static readonly Dictionary<string, CharacterManifest> Manifests = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static AnimationClip2D<Texture2D> LoadClip(TextureCache2D textures, string characterId, string animationId)
    {
        ArgGuard.ThrowIfNull(textures);
        AssetId2D.Validate(characterId);
        AssetId2D.Validate(animationId);

        var manifest = LoadManifest(textures.ContentRoot, characterId);
        if (!manifest.Animations.TryGetValue(animationId, out var animation))
        {
            throw new InvalidDataException($"Character '{characterId}' does not define animation '{animationId}'.");
        }

        var relativeDirectory = Path.Combine("characters", characterId, "animations", animationId);
        var fullDirectory = Path.Combine(textures.ContentRoot, relativeDirectory);
        var framePaths = Directory.EnumerateFiles(fullDirectory, "frame-*.png").Order(StringComparer.Ordinal).ToArray();
        if (framePaths.Length == 0)
        {
            throw new InvalidDataException($"Animation '{characterId}/{animationId}' has no frames.");
        }

        for (var i = 0; i < framePaths.Length; i++)
        {
            var expectedName = $"frame-{i + 1:0000}.png";
            if (!string.Equals(Path.GetFileName(framePaths[i]), expectedName, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Animation '{characterId}/{animationId}' must contain contiguous four-digit frames beginning at frame-0001.png; expected '{expectedName}'.");
            }
        }

        var framesPerSecond = animation.DurationSeconds is { } duration
            ? framePaths.Length / duration
            : animation.FramesPerSecond!.Value;
        return new AnimationClip2D<Texture2D>(framePaths.Select(path => textures.Load(Path.GetRelativePath(textures.ContentRoot, path))), framesPerSecond, animation.Loop);
    }

    public static float LoadHorizontalRootOffsetFraction(TextureCache2D textures, string characterId, string animationId, string facingId)
    {
        ArgGuard.ThrowIfNull(textures);
        AssetId2D.Validate(characterId);
        AssetId2D.Validate(animationId);
        AssetId2D.Validate(facingId);

        var manifest = LoadManifest(textures.ContentRoot, characterId);
        if (!manifest.Animations.TryGetValue(animationId, out var animation))
        {
            throw new InvalidDataException($"Character '{characterId}' does not define animation '{animationId}'.");
        }
        if (animation.CanvasWidth is not { } canvasWidth || animation.RootXByFacing is null)
        {
            return 0f;
        }
        if (!animation.RootXByFacing.TryGetValue(facingId, out var rootX))
        {
            throw new InvalidDataException($"Animation '{characterId}/{animationId}' does not define root X for facing '{facingId}'.");
        }

        return (canvasWidth * 0.5f - rootX) / canvasWidth;
    }

    private static CharacterManifest LoadManifest(string contentRoot, string characterId)
    {
        var characterRoot = Path.Combine(contentRoot, "characters", characterId);
        var manifestPath = Path.Combine(characterRoot, "character.json");
        lock (ManifestLock)
        {
            if (Manifests.TryGetValue(manifestPath, out var cached))
                return cached;

            if (!File.Exists(manifestPath))
                throw new FileNotFoundException("Character manifest was not found.", manifestPath);

            var manifest = JsonSerializer.Deserialize<CharacterManifest>(File.ReadAllText(manifestPath), JsonOptions) ??
                throw new InvalidDataException($"Character manifest is empty: {manifestPath}");
            ValidateManifest(manifest, characterId, characterRoot, manifestPath);
            Manifests.Add(manifestPath, manifest);
            return manifest;
        }
    }

    private static void ValidateManifest(CharacterManifest manifest, string characterId, string characterRoot, string manifestPath)
    {
        if (!string.Equals(manifest.Id, characterId, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Manifest ID '{manifest.Id}' must match its folder '{characterId}': {manifestPath}");
        }
        if (manifest.Animations.Count == 0)
            throw new InvalidDataException($"Character has no animations: {manifestPath}");

        foreach (var (animationId, animation) in manifest.Animations)
        {
            AssetId2D.Validate(animationId, $"{manifestPath}:animations");
            var hasFramesPerSecond = animation.FramesPerSecond is { } framesPerSecond && float.IsFinite(framesPerSecond) && framesPerSecond > 0f;
            var hasDuration = animation.DurationSeconds is { } duration && float.IsFinite(duration) && duration > 0f;
            if (hasFramesPerSecond == hasDuration)
            {
                throw new InvalidDataException($"Animation '{characterId}/{animationId}' must define exactly one of framesPerSecond or durationSeconds.");
            }

            var canvasWidth = animation.CanvasWidth ?? 0f;
            var hasCanvasWidth = animation.CanvasWidth is not null &&
                float.IsFinite(canvasWidth) && canvasWidth > 0f;
            var hasDirectionalRoots = animation.RootXByFacing is { Count: > 0 };
            if (hasCanvasWidth != hasDirectionalRoots)
            {
                throw new InvalidDataException($"Animation '{characterId}/{animationId}' must define canvasWidth and rootXByFacing together.");
            }
            if (hasDirectionalRoots)
            {
                foreach (var facingId in new[] { "right", "left" })
                {
                    if (!animation.RootXByFacing!.TryGetValue(facingId, out var rootX) || !float.IsFinite(rootX) || rootX < 0f || rootX > canvasWidth)
                    {
                        throw new InvalidDataException($"Animation '{characterId}/{animationId}' has invalid root X for facing '{facingId}'.");
                    }
                }
            }

            var animationDirectory = Path.Combine(characterRoot, "animations", animationId);
            if (!Directory.Exists(animationDirectory))
            {
                throw new DirectoryNotFoundException($"Animation folder was not found: {animationDirectory}");
            }
        }

        var animationsRoot = Path.Combine(characterRoot, "animations");
        var manifestIds = manifest.Animations.Keys.ToHashSet(StringComparer.Ordinal);
        var unlistedDirectory = Directory
            .EnumerateDirectories(animationsRoot)
            .Select(Path.GetFileName)
            .FirstOrDefault(id => id is not null && !manifestIds.Contains(id));
        if (unlistedDirectory is not null)
        {
            throw new InvalidDataException($"Animation folder '{characterId}/{unlistedDirectory}' is not listed in {manifestPath}");
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class CharacterManifest
    {
        public string Id { get; init; } = string.Empty;
        public Dictionary<string, AnimationManifest> Animations { get; init; } = new(StringComparer.Ordinal);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1812:Avoid uninstantiated internal classes",
        Justification = "System.Text.Json creates manifest objects through reflection.")]
    private sealed class AnimationManifest
    {
        public float? FramesPerSecond { get; init; }
        public float? DurationSeconds { get; init; }
        public bool Loop { get; init; }
        public float? CanvasWidth { get; init; }
        public Dictionary<string, float>? RootXByFacing { get; init; }
    }
}
