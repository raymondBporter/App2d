using App2d.Core.Validation;
using App2d.Core.IO;

namespace App2d.Core.Assets;

/// <summary>
/// The main App2d resource locations. Hosts select roots once and pass paths to loaders;
/// loaders do not search for the repository. See Assets/README.md for folder ownership.
/// Construct explicitly for custom layouts, or use the discovery helpers for existing app layouts.
/// </summary>
public sealed class AssetLocations
{
    public AssetLocations(string runtime, string authored, string characterLibrary)
    {
        Runtime = Path.GetFullPath(ArgGuard.RequireNotNullOrWhiteSpace(runtime));
        Authored = Path.GetFullPath(ArgGuard.RequireNotNullOrWhiteSpace(authored));
        CharacterLibrary = Path.GetFullPath(ArgGuard.RequireNotNullOrWhiteSpace(characterLibrary));
    }

    /// <summary>Generated art and copied runtime inputs. Assets/Runtime in a source checkout; disposable.</summary>
    public string Runtime { get; }
    /// <summary>Durable content such as levels and music. Assets/Static in a source checkout.</summary>
    public string Authored { get; }
    /// <summary>Character source libraries and authored documents. Assets/Characters in a source checkout.</summary>
    public string CharacterLibrary { get; }
    public string AuthoredCharacters => FilePaths.ResolveUnderRoot(CharacterLibrary, "authored");
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
        var sourceCharacters = Path.GetFullPath(Path.Combine(runtime, "..", "Characters"));
        var characters = useSourceAssets && Directory.Exists(Path.Combine(sourceCharacters, "authored"))
            ? sourceCharacters : FilePaths.ResolveUnderRoot(runtime, "PointCharacters");
        return new(runtime, authored, characters);
    }

    /// <summary>
    /// Character Studio's existing layout: prefer its packaged Assets/Characters, then search above
    /// the working directory. Editors can also accept an explicit authoring root instead of discovery.
    /// </summary>
    public static string FindCharacterLibrary(string baseDirectory, string workingDirectory)
    {
        var packaged = FilePaths.ResolveUnderRoot(baseDirectory, "Assets/Characters");
        if (Directory.Exists(Path.Combine(packaged, "authored"))) return packaged;
        var authored = FindAncestorDirectory(workingDirectory, "Assets/Characters/authored");
        return authored is not null ? Path.GetDirectoryName(authored)! :
            throw new DirectoryNotFoundException("Assets/Characters/authored was not found beside the executable or above the working directory.");
    }

    private static string? FindAncestorDirectory(string start, string relativePath)
    {
        ArgGuard.ThrowIfNullOrWhiteSpace(start);
        for (var directory = new DirectoryInfo(Path.GetFullPath(start)); directory is not null; directory = directory.Parent)
        {
            var candidate = FilePaths.ResolveUnderRoot(directory.FullName, relativePath);
            if (Directory.Exists(candidate)) return candidate;
        }
        return null;
    }
}
