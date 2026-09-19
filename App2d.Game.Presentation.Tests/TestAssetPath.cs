namespace App2d.Gameplay.Tests;

internal static class TestAssetPath
{
    public static string Root { get; } = Find("Runtime");

    /// <summary>The durable, hand-authored assets root (as opposed to the disposable <see cref="Root"/>).</summary>
    public static string StaticRoot { get; } = Find("Static");

    private static string Find(string assetsSubdirectory)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "Assets", assetsSubdirectory);
            if (Directory.Exists(candidate))
                return candidate;
        }

        throw new InvalidOperationException(
            $"Assets/{assetsSubdirectory} was not found. Run: python tools/ArtPipeline/build_runtime_assets.py");
    }
}
