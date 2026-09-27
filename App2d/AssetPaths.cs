namespace App2d;

internal static class AssetPaths
{
    public static string Root { get; } = FindRoot();
    // Authored content must not depend on an older, ignored pipeline output.
    public static string AuthoredRoot { get; } = FindAuthoredRoot();
    public static string Music => Path.Combine(AuthoredRoot, "audio", "music");
    public static string Characters => Directory.Exists(Path.Combine(Root, "..", "Characters", "authored"))
        ? Path.GetFullPath(Path.Combine(Root, "..", "Characters")) : Path.Combine(Root, "PointCharacters");

    private static string FindAuthoredRoot()
    {
#if DEBUG
        return ResolveAuthoredRoot(AppContext.BaseDirectory, Root, useSourceAssets: true);
#else
        return ResolveAuthoredRoot(AppContext.BaseDirectory, Root, useSourceAssets: false);
#endif
    }

    internal static string ResolveAuthoredRoot(string baseDirectory, string runtimeRoot, bool useSourceAssets)
    {
        for (var directory = useSourceAssets ? new DirectoryInfo(baseDirectory) : null;
             directory is not null;
             directory = directory.Parent)
        {
            var sourceRoot = Path.Combine(directory.FullName, "Assets", "Static");
            if (Directory.Exists(sourceRoot))
                return sourceRoot;
        }
        return runtimeRoot;
    }

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
