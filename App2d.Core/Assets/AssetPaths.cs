namespace App2d.Core.Assets;

/// <summary>The game host's resolved locations. Shared layout and discovery live in Core.</summary>
internal static class AssetPaths
{
    public static AssetLocations Current { get; } = AssetLocations.ForGame(AppContext.BaseDirectory,
#if DEBUG
        useSourceAssets: true);
#else
        useSourceAssets: false);
#endif
}
