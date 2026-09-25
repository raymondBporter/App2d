namespace App2d.CharacterStudio;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            var root = FindAssets(args.Contains("--workshop") || args.Contains("--smoke-workshop"));
            if (args is ["--check"]) { StudioChecks.Run(root); return 0; }
            if (args.Length != 0 && args is not ["--workshop"] && args is not ["--smoke" or "--smoke-wolf" or "--smoke-workshop", _]) throw new ArgumentException("Usage: App2d.CharacterStudio [--workshop | --check | --smoke output-directory | --smoke-wolf output-directory | --smoke-workshop output-directory]");
            using var studio = new StudioGame(root, args.Length == 2 ? Path.GetFullPath(args[1]) : null, args is ["--smoke-wolf", _], args is ["--workshop"], args is ["--smoke-workshop", _]); studio.Run(); return 0;
        }
        catch (Exception ex)
        {
            var log = Path.Combine(AppContext.BaseDirectory, "character-studio-error.log"); File.WriteAllText(log, ex.ToString());
            Console.Error.WriteLine(ex);
            if (args.Length == 0) MessageBox.Show(ex.Message + "\n\nDetails: " + log, "Character Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
    private static string FindAssets(bool optional = false)
    {
        var packaged = Path.Combine(AppContext.BaseDirectory, "Assets", "Characters");
        if (File.Exists(Path.Combine(packaged, "catalog.json"))) return packaged;
        for (var folder = new DirectoryInfo(Environment.CurrentDirectory); folder is not null; folder = folder.Parent)
        { var root = Path.Combine(folder.FullName, "Assets", "Characters"); if (File.Exists(Path.Combine(root, "catalog.json"))) return root; }
        if (optional) return packaged;
        throw new DirectoryNotFoundException("Character assets are missing. Use --workshop to create a character without imported assets, or run node tools/CharacterPipeline/import.cjs <sprite-renderer-path> and rebuild.");
    }
}
