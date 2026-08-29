using System.Collections.Concurrent;
using App2d.Engine.Rendering.Textures;

namespace App2d.Gameplay;

internal static class EquippedLoadoutAnimationAssets2D
{
    private static readonly ConcurrentDictionary<string, string[]> FrameCatalogs = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    public static void ValidateEquipmentAnimation(TextureCache2D textures, string equipmentId, string animationId, int expectedFrameCount, string facingId)
    {
        ArgGuard.ThrowIfNull(textures);
        AssetId2D.Validate(equipmentId);
        AssetId2D.Validate(animationId);
        AssetId2D.Validate(facingId);
        ArgGuard.ThrowIfNotPositive(expectedFrameCount);
        _ = FindEquipmentFrames(textures.ContentRoot, equipmentId, animationId, facingId, expectedFrameCount);
    }

    public static Texture2D LoadFrame(TextureCache2D textures, string characterId, string animationId, int expectedFrameCount, string facingId, int frameIndex, params string[] equipmentIds)
    {
        ArgGuard.ThrowIfNull(textures);
        ArgGuard.ThrowIfNull(equipmentIds);
        AssetId2D.Validate(characterId);
        AssetId2D.Validate(animationId);
        AssetId2D.Validate(facingId);
        ArgGuard.ThrowIfNotPositive(expectedFrameCount);
        ArgGuard.ThrowIfTooShort(equipmentIds.AsSpan(), 1);
        ArgGuard.ThrowIfGreaterThanOrEqual(frameIndex, expectedFrameCount);
        foreach (var equipmentId in equipmentIds)
            AssetId2D.Validate(equipmentId);

        var characterRoot = Path.Combine("characters", characterId, "animations", animationId);
        var characterColor = FindFrames(textures.ContentRoot, Path.Combine(characterRoot, "color", facingId), expectedFrameCount);
        var characterDepth = FindFrames(textures.ContentRoot, Path.Combine(characterRoot, "depth", facingId), expectedFrameCount);
        var equipment = equipmentIds
            .Select(id => FindEquipmentFrames(textures.ContentRoot, id, animationId, facingId, expectedFrameCount))
            .ToArray();
        return ComposeFrame(characterColor[frameIndex], characterDepth[frameIndex], equipment, frameIndex);
    }

    public static TextureFrameSet2D LoadCached(TextureCache2D textures, string loadoutId, string animationId, int expectedFrameCount, string facingId)
    {
        ArgGuard.ThrowIfNull(textures);
        AssetId2D.Validate(loadoutId);
        AssetId2D.Validate(animationId);
        AssetId2D.Validate(facingId);
        ArgGuard.ThrowIfNotPositive(expectedFrameCount);

        var paths = FindFrames(textures.ContentRoot, Path.Combine("loadouts", loadoutId, "animations", animationId, facingId), expectedFrameCount);
        return new TextureFrameSet2D(textures, paths.Select(path => Path.GetRelativePath(textures.ContentRoot, path)).ToArray());
    }

    public static Texture2D[] Load(TextureCache2D textures, string characterId, string animationId, int expectedFrameCount, string facingId, params string[] equipmentIds)
    {
        ArgGuard.ThrowIfNull(textures);
        ArgGuard.ThrowIfNull(equipmentIds);
        AssetId2D.Validate(characterId);
        AssetId2D.Validate(animationId);
        AssetId2D.Validate(facingId);
        ArgGuard.ThrowIfNotPositive(expectedFrameCount);
        ArgGuard.ThrowIfTooShort(equipmentIds.AsSpan(), 1);
        foreach (var equipmentId in equipmentIds)
            AssetId2D.Validate(equipmentId);

        var characterRoot = Path.Combine("characters", characterId, "animations", animationId);
        var characterColor = FindFrames(textures.ContentRoot, Path.Combine(characterRoot, "color", facingId), expectedFrameCount);
        var characterDepth = FindFrames(textures.ContentRoot, Path.Combine(characterRoot, "depth", facingId), expectedFrameCount);
        var equipment = equipmentIds
            .Select(id => FindEquipmentFrames(textures.ContentRoot, id, animationId, facingId, expectedFrameCount))
            .ToArray();

        var frames = new Texture2D[expectedFrameCount];
        try
        {
            for (var frameIndex = 0; frameIndex < frames.Length; frameIndex++)
            {
                frames[frameIndex] = ComposeFrame(characterColor[frameIndex], characterDepth[frameIndex], equipment, frameIndex);
            }
            return frames;
        }
        catch
        {
            foreach (var frame in frames)
                frame?.Dispose();
            throw;
        }
    }

    private static Texture2D ComposeFrame(string characterColorPath, string characterDepthPath, EquipmentFramePaths[] equipment, int frameIndex)
    {
        var loaded = new Texture2D[(equipment.Length + 1) * 2];
        try
        {
            loaded[0] = Texture2D.Load(characterColorPath);
            loaded[1] = Texture2D.Load(characterDepthPath);
            var layers = new DepthTextureLayer2D[equipment.Length + 1];
            layers[0] = new DepthTextureLayer2D(loaded[0], loaded[1]);
            for (var equipmentIndex = 0; equipmentIndex < equipment.Length; equipmentIndex++)
            {
                var loadedIndex = (equipmentIndex + 1) * 2;
                loaded[loadedIndex] = Texture2D.Load(equipment[equipmentIndex].Color[frameIndex]);
                loaded[loadedIndex + 1] = Texture2D.Load(equipment[equipmentIndex].Depth[frameIndex]);
                layers[equipmentIndex + 1] = new DepthTextureLayer2D(loaded[loadedIndex], loaded[loadedIndex + 1]);
            }
            return DepthCompositeTexture2D.Create(layers);
        }
        finally
        {
            foreach (var texture in loaded)
                texture?.Dispose();
        }
    }

    private static EquipmentFramePaths FindEquipmentFrames(string contentRoot, string equipmentId, string animationId, string facingId, int expectedFrameCount)
    {
        var root = Path.Combine("weapons", equipmentId, "animations", animationId, facingId);
        return new EquipmentFramePaths(
            FindFrames(contentRoot, Path.Combine(root, "color"), expectedFrameCount),
            FindFrames(contentRoot, Path.Combine(root, "depth"), expectedFrameCount));
    }

    private static string[] FindFrames(string contentRoot, string relativeDirectory, int expectedFrameCount)
    {
        var fullDirectory = Path.GetFullPath(Path.Combine(contentRoot, relativeDirectory));
        var paths = FrameCatalogs
            .GetOrAdd(fullDirectory, static directory => Directory.EnumerateFiles(directory, "frame-*.png").Order(StringComparer.Ordinal)
            .ToArray());
        if (paths.Length != expectedFrameCount)
        {
            throw new InvalidDataException($"Expected {expectedFrameCount} frames in '{relativeDirectory}', found {paths.Length}.");
        }
        return paths;
    }

    private sealed record EquipmentFramePaths(string[] Color, string[] Depth);
}
