using App2d.Core.Validation;
using App2d.Core.IO;

namespace App2d.Core.Assets;

/// <summary>Writable per-user state, kept outside source assets and installed content. Merely resolving paths creates nothing.</summary>
public sealed class UserDataLocations(string root)
{
    public string Root { get; } = Path.GetFullPath(ArgGuard.RequireNotNullOrWhiteSpace(root));
    public string PlayerSave => FilePaths.ResolveUnderRoot(Root, "save.json");
    public string CharacterStudioSettings => FilePaths.ResolveUnderRoot(Root, "CharacterStudio/settings.json");

    public static UserDataLocations ForCurrentUser() => new(FilePaths.ResolveUnderRoot(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "App2d"));
}
