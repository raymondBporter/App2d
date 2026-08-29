using App2d.Engine.Rendering.Textures;

namespace App2d.Gameplay;

internal static class DirectionalCharacterAnimationAssets2D
{
    public static TextureFrameSet2D LoadFacing(TextureCache2D textures, string characterId, string animationId, string facingId, int expectedFrameCount)
    {
        ArgGuard.ThrowIfNull(textures);
        AssetId2D.Validate(characterId);
        AssetId2D.Validate(animationId);
        AssetId2D.Validate(facingId);
        ArgGuard.ThrowIfNotPositive(expectedFrameCount);

        var relativeDirectory = Path.Combine("characters", characterId, "animations", animationId, "color", facingId);
        var fullDirectory = Path.Combine(textures.ContentRoot, relativeDirectory);
        var paths = Directory
            .EnumerateFiles(fullDirectory, "frame-*.png")
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (paths.Length != expectedFrameCount)
        {
            throw new InvalidDataException(
                $"Expected {expectedFrameCount} directional character frames in " +
                $"'{relativeDirectory}', found {paths.Length}.");
        }

        return new TextureFrameSet2D(textures, paths.Select(path => Path.GetRelativePath(textures.ContentRoot, path)).ToArray());
    }
}
