namespace App2d;

internal static class AssetPaths
{
    public static string Root { get; } = FindRoot();
    public static string Characters => Directory.Exists(Path.Combine(Root, "..", "Characters", "entities"))
        ? Path.GetFullPath(Path.Combine(Root, "..", "Characters")) : Path.Combine(Root, "PointCharacters");

    private static string FindRoot()
    {
#if DEBUG
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var sourceRoot = Path.Combine(directory.FullName, "Assets", "Runtime");
            if (Directory.Exists(sourceRoot))
                return sourceRoot;
        }
#endif

        return Path.Combine(AppContext.BaseDirectory, "Assets");
    }
}
