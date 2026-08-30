namespace App2d.Tests;

internal static class TestAssets
{
    private static readonly string Root = FindRoot();

    public static string GetPath(params string[] segments) =>
        Path.Combine([Root, .. segments]);

    private static string FindRoot()
    {
        foreach (var startPath in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(Path.GetFullPath(startPath));
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "Assets");
                if (Directory.Exists(Path.Combine(candidate, "Runtime")))
                {
                    return candidate;
                }
                directory = directory.Parent;
            }
        }
        throw new DirectoryNotFoundException("Could not locate the App2d Assets directory.");
    }
}
