using App2d.CharacterSeeds;
using App2d.Core.Assets;
using App2d.Core.Validation;

namespace App2d.CharacterStudio;

internal static class Program
{
    private const string Usage = "Usage: App2d.CharacterStudio [--write-pterosaur authored-directory | --smoke-pterosaur output-directory | --write-quadrupeds authored-directory | --smoke-quadrupeds output-directory | --smoke-editor output-directory | --smoke-editor-surface output-directory | --smoke-acceptance output-directory | --smoke-motion output-directory | --smoke-entities output-directory | --smoke-weapons output-directory | --smoke-wardrobe output-directory | --review-moves output-directory | --swing-lab output-directory | --convert-studies authored-directory [--force] | --write-player-moves authored-directory [--force] | --write-weapons authored-directory [--force] | --write-wardrobe authored-directory [--force] | --replace-wardrobe-art authored-directory | --smoke-rounded-paths output-directory]";

    [STAThread]
    private static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        try
        {
            if (SeedCommands.Handles(args))
                return SeedCommands.Run(args);

            switch (args)
            {
                case ["--smoke-rounded-paths", var curveOutput]:
                    using (var proofs = new ProofRenders(FindAssets(preferSource: true), Path.GetFullPath(curveOutput), ProofRenders.Mode.RoundedPaths))
                    {
                        proofs.Run();
                    }

                    return 0;
                case ["--write-pterosaur", var flyingRoot, .. var flyingFlags] when Flags(flyingFlags):
                    return Report(report => PterosaurTemplate.Write(Path.GetFullPath(flyingRoot), report), flyingFlags);
                case ["--smoke-editor-surface", var surfaceOutput]:
                    Editor.EditorSurfaceSmoke.Run(Path.Combine(FindAssets(preferSource: true), "authored"), Path.GetFullPath(surfaceOutput));
                    return 0;
                case ["--write-quadrupeds", var quadRoot, .. var quadFlags] when Flags(quadFlags):
                    return Report(report => QuadrupedTemplate.Write(Path.GetFullPath(quadRoot), report), quadFlags);
                case ["--convert-studies", var output, .. var studyFlags] when Flags(studyFlags):
                    return Report(report => PersonTemplate.WriteStudies(Path.GetFullPath(output), report), studyFlags);
                case ["--write-player-moves", var moves, .. var moveFlags] when Flags(moveFlags):
                    return Report(report => PlayerMoves.PlayerMoves.Write(Path.GetFullPath(moves), report), moveFlags);
                case ["--write-spells", var spells, .. var spellFlags] when Flags(spellFlags):
                    return Report(report => PlayerMoves.PlayerMoves.WriteSpells(Path.GetFullPath(spells), report), spellFlags);
                case ["--write-weapons", var root, .. var weaponFlags] when Flags(weaponFlags):
                    return Report(report => PlayerMoves.PlayerMoves.WriteWeapons(Path.GetFullPath(root), report), weaponFlags);
                case ["--write-wardrobe", var root, .. var wardrobeFlags] when Flags(wardrobeFlags):
                    return Report(report => PersonWardrobe.Write(Path.GetFullPath(root), report: report), wardrobeFlags);
                case ["--replace-wardrobe-art", var root]:
                    return Report(report => PersonWardrobe.Write(Path.GetFullPath(root), replaceExistingProps: true, report: report), ["--force"]);
                case ["--smoke-acceptance", var acceptanceOutput]:
                    {
                        // P11c: the migration plan's section 6 reference workflow on a scratch copy, a frame per step.
                        using var editor = new Editor.EditorApp(Path.Combine(FindAssets(preferSource: true), "authored"), Path.GetFullPath(acceptanceOutput), acceptance: true);
                        editor.Run();
                        return 0;
                    }
                case [] or ["--editor"] or ["--smoke-editor", _]:
                    {
                        using var editor = new Editor.EditorApp(Path.Combine(FindAssets(preferSource: true), "authored"), args.Length == 2 ? Path.GetFullPath(args[1]) : null);
                        editor.Run();
                        return 0;
                    }
                case ["--smoke-pterosaur" or "--smoke-quadrupeds" or "--smoke-motion" or "--smoke-entities" or "--review-moves" or "--smoke-weapons" or "--smoke-wardrobe" or "--swing-lab", var output]:
                    {
                        var mode = args[0] switch
                        {
                            "--smoke-pterosaur" => ProofRenders.Mode.Pterosaur,
                            "--smoke-quadrupeds" => ProofRenders.Mode.Quadrupeds,
                            "--smoke-motion" => ProofRenders.Mode.Motion,
                            "--smoke-entities" => ProofRenders.Mode.Entities,
                            "--smoke-weapons" => ProofRenders.Mode.Weapons,
                            "--smoke-wardrobe" => ProofRenders.Mode.Wardrobe,
                            "--swing-lab" => ProofRenders.Mode.SwingLab,
                            _ => ProofRenders.Mode.MoveReview
                        };
                        using var proofs = new ProofRenders(FindAssets(), Path.GetFullPath(output), mode);
                        proofs.Run();
                        return 0;
                    }
                default:
                    throw ArgGuard.CreateInvalid(Usage + Environment.NewLine + SeedCommands.Usage);
            }
        }
        catch (Exception ex)
        {
            var log = Path.Combine(AppContext.BaseDirectory, "character-studio-error.log");
            File.WriteAllText(log, ex.ToString());
            Console.Error.WriteLine(ex);
            if (args.Length == 0)
            {
                MessageBox.Show(ex.Message + "\n\nDetails: " + log, "Character Studio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return 1;
        }
    }

    /// <summary>A write command's optional trailing flag: nothing, or --force to replace editor-owned files (P11b).</summary>
    private static bool Flags(string[] flags) => flags is [] or ["--force"];

    /// <summary>Runs a seed writer under the writing policy and prints what it touched.</summary>
    private static int Report(Action<SeedReport> write, string[] flags)
    {
        var report = new SeedReport(flags.Length == 1);
        write(report);
        foreach (var line in report.Lines)
        {
            Console.WriteLine(line);
        }

        Console.WriteLine(report + (report.Count(SeedReport.Kept) > 0 ? " (kept files are editor-owned; pass --force to replace them)" : ""));
        return 0;
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
                if (Directory.Exists(Path.Combine(root, "authored")))
                {
                    return root;
                }
            }
        }

        return AssetLocations.FindCharacterLibrary(AppContext.BaseDirectory, Environment.CurrentDirectory);
    }
}
