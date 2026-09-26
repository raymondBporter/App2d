namespace App2d.CharacterStudio;

internal static class Program
{
    private const string Usage = "Usage: App2d.CharacterStudio [--smoke-editor output-directory | --smoke-motion output-directory | --smoke-entities output-directory | --review-moves output-directory | --convert-studies authored-directory | --write-player-moves authored-directory]";

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            switch (args)
            {
                case ["--convert-studies", var output]: Core.Characters.PersonTemplate.WriteStudies(Path.GetFullPath(output)); return 0;
                case ["--write-player-moves", var moves]: PlayerMoves.PlayerMoves.Write(Path.GetFullPath(moves)); return 0;
                case [] or ["--editor"] or ["--smoke-editor", _]:
                {
                    using var editor = new Editor.EditorApp(Path.Combine(FindAssets(), "authored"), args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                    editor.Run(); return 0;
                }
                case ["--smoke-motion" or "--smoke-entities" or "--review-moves", var output]:
                {
                    var mode = args[0] switch { "--smoke-motion" => ProofRenders.Mode.Motion, "--smoke-entities" => ProofRenders.Mode.Entities, _ => ProofRenders.Mode.MoveReview };
                    using var proofs = new ProofRenders(FindAssets(), Path.GetFullPath(output), mode);
                    proofs.Run(); return 0;
                }
                default: throw new ArgumentException(Usage);
            }
        }
        catch (Exception ex)
        {
            var log = Path.Combine(AppContext.BaseDirectory, "character-studio-error.log"); File.WriteAllText(log, ex.ToString());
            Console.Error.WriteLine(ex);
            if (args.Length == 0) MessageBox.Show(ex.Message + "\n\nDetails: " + log, "Character Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }

    /// <summary>The characters folder: packaged beside the executable, or the nearest Assets/Characters above the working directory.</summary>
    private static string FindAssets()
    {
        var packaged = Path.Combine(AppContext.BaseDirectory, "Assets", "Characters");
        if (Directory.Exists(Path.Combine(packaged, "authored"))) return packaged;
        for (var folder = new DirectoryInfo(Environment.CurrentDirectory); folder is not null; folder = folder.Parent)
        { var root = Path.Combine(folder.FullName, "Assets", "Characters"); if (Directory.Exists(Path.Combine(root, "authored"))) return root; }
        throw new DirectoryNotFoundException("Assets/Characters/authored was not found above the working directory.");
    }
}
