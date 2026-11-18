using System.Reflection;

namespace App2d.Core.Assets;

/// <summary>The game host's resolved locations. Shared layout and discovery live in Core.</summary>
internal static class AssetPaths
{
    public static AssetLocations Current { get; } = FindLocations();

    private static AssetLocations FindLocations()
    {
#if DEBUG
        // Tools in sibling checkouts still use this checkout's game content.
        var sourceRoot = typeof(AssetPaths).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "App2dRoot").Value!;
        var baseDirectory = Directory.Exists(Path.Combine(sourceRoot, "Assets"))
            ? Path.Combine(sourceRoot, "App2d") : AppContext.BaseDirectory;
        return AssetLocations.ForGame(baseDirectory, useSourceAssets: true);
#else
        return AssetLocations.ForGame(AppContext.BaseDirectory, useSourceAssets: false);
#endif
    }
}
