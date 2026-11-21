using App2d.Core.IO;
using App2d.Core.Validation;

namespace App2d.Core.Assets;

/// <summary>
/// The main App2d resource locations. Hosts select roots once and pass paths to loaders;
/// loaders do not search for the repository. See Assets/README.md for folder ownership.
/// Construct explicitly for custom layouts, or use the discovery helpers for existing app layouts.
/// </summary>
public sealed class AssetLocations(string runtime, string authored)
{

    /// <summary>Generated art and copied runtime inputs. Assets/Runtime in a source checkout; disposable.</summary>
    public string Runtime { get; } = Path.GetFullPath(Ensure.RequireNotNullOrWhiteSpace(runtime));
    /// <summary>Durable content such as levels and music. Assets/Static in a source checkout.</summary>
    public string Authored { get; } = Path.GetFullPath(Ensure.RequireNotNullOrWhiteSpace(authored));
    public string Levels => FilePaths.ResolveUnderRoot(Authored, "levels");
    public string Music => FilePaths.ResolveUnderRoot(Authored, "audio/music");
    public string SoundEffects => FilePaths.ResolveUnderRoot(Runtime, "audio/sfx");
    public string Tilesets => FilePaths.ResolveUnderRoot(Runtime, "environments/tilesets");
    public string UI => FilePaths.ResolveUnderRoot(Runtime, "ui");

    /// <summary>
    /// Debug can find source assets above the executable; deployed games use only their packaged Assets.
    /// Authored content is resolved independently so pulled music/levels do not require an art rebuild.
    /// </summary>
    public static AssetLocations ForGame(string baseDirectory, bool useSourceAssets)
    {
        var packaged = FilePaths.ResolveUnderRoot(baseDirectory, "Assets");
        var runtime = useSourceAssets ? FindAncestorDirectory(baseDirectory, "Assets/Runtime") ?? packaged : packaged;
        var authored = useSourceAssets ? FindAncestorDirectory(baseDirectory, "Assets/Static") ?? runtime : runtime;
        return new(runtime, authored);
    }

    private static string? FindAncestorDirectory(string start, string relativePath)
    {
        Ensure.ThrowIfNullOrWhiteSpace(start);
        for (var directory = new DirectoryInfo(Path.GetFullPath(start)); directory is not null; directory = directory.Parent)
        {
            var candidate = FilePaths.ResolveUnderRoot(directory.FullName, relativePath);
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }
}
