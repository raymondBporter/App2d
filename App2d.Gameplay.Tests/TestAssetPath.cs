namespace App2d.Gameplay.Tests;

internal static class TestAssetPath
{
    public static string Root { get; } = Find();

    private static string Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var runtimeRoot = Path.Combine(directory.FullName, "Assets", "Runtime");
            if (Directory.Exists(runtimeRoot))
                return runtimeRoot;
        }

        throw new InvalidOperationException(
            "Assets/Runtime was not found. Run: python tools/ArtPipeline/build_runtime_assets.py");
    }
}
