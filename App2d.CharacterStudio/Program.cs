using App2d.Core.Assets;
using App2d.Core.Characters.Authored;
using App2d.Core.Validation;

namespace App2d.CharacterStudio;

internal static class Program
{
    private const string Usage = "Usage: App2d.CharacterStudio [--migrate-materials authored-directory | --write-quadrupeds authored-directory | --smoke-quadrupeds output-directory | --smoke-editor output-directory | --smoke-motion output-directory | --smoke-entities output-directory | --smoke-weapons output-directory | --smoke-wardrobe output-directory | --review-moves output-directory | --swing-lab output-directory | --convert-studies authored-directory | --write-player-moves authored-directory | --write-weapons authored-directory | --write-wardrobe authored-directory | --replace-wardrobe-art authored-directory | --angelia-gallery [output-directory]]";

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            switch (args)
            {
                case ["--migrate-materials", var materialRoot]: AuthoredMaterialMigration.Run(Path.GetFullPath(materialRoot)); return 0;
                case ["--write-quadrupeds", var quadRoot]: QuadrupedTemplate.Write(Path.GetFullPath(quadRoot)); return 0;
                case ["--convert-studies", var output]: PersonTemplate.WriteStudies(Path.GetFullPath(output)); return 0;
                case ["--write-player-moves", var moves]: PlayerMoves.PlayerMoves.Write(Path.GetFullPath(moves)); return 0;
                case ["--write-spells", var spells]: PlayerMoves.PlayerMoves.WriteSpells(Path.GetFullPath(spells)); return 0;
                case ["--write-weapons", var root]: PlayerMoves.PlayerMoves.WriteWeapons(Path.GetFullPath(root)); return 0;
                case ["--write-wardrobe", var root]: PersonWardrobe.Write(Path.GetFullPath(root)); return 0;
                case ["--replace-wardrobe-art", var root]: PersonWardrobe.Write(Path.GetFullPath(root), replaceExistingProps: true); return 0;
                case [] or ["--editor"] or ["--smoke-editor", _]:
                    {
                        using var editor = new Editor.EditorApp(Path.Combine(FindAssets(preferSource: true), "authored"), args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                        editor.Run(); return 0;
                    }
                case ["--angelia-gallery"] or ["--angelia-gallery", _]:
                    {
                        using var gallery = new AngeliaGallery.AngeliaGalleryApp(FindAssets(), args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                        gallery.Run(); return 0;
                    }
                case ["--smoke-quadrupeds" or "--smoke-motion" or "--smoke-entities" or "--review-moves" or "--smoke-weapons" or "--smoke-wardrobe" or "--swing-lab", var output]:
                    {
                        var mode = args[0] switch { "--smoke-quadrupeds" => ProofRenders.Mode.Quadrupeds, "--smoke-motion" => ProofRenders.Mode.Motion, "--smoke-entities" => ProofRenders.Mode.Entities, "--smoke-weapons" => ProofRenders.Mode.Weapons, "--smoke-wardrobe" => ProofRenders.Mode.Wardrobe, "--swing-lab" => ProofRenders.Mode.SwingLab, _ => ProofRenders.Mode.MoveReview };
                        using var proofs = new ProofRenders(FindAssets(), Path.GetFullPath(output), mode);
                        proofs.Run(); return 0;
                    }
                default: throw ArgGuard.CreateInvalid(Usage);
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

    /// <summary>
    /// The characters folder. The editor prefers the source checkout above the working directory,
    /// so saves land in Assets/Characters rather than the build output copy.
    /// </summary>
    private static string FindAssets(bool preferSource = false)
    {
        if (preferSource)
        {
            for (var folder = new DirectoryInfo(Environment.CurrentDirectory); folder is not null; folder = folder.Parent)
            {
                var root = Path.Combine(folder.FullName, "Assets", "Characters");
                if (Directory.Exists(Path.Combine(root, "authored"))) return root;
            }
        }

        return AssetLocations.FindCharacterLibrary(AppContext.BaseDirectory, Environment.CurrentDirectory);
    }
}
