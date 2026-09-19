namespace App2d.Noodle;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.SequenceEqual(["--check"]))
        {
            RigidPrototypeChecks2D.Run();
            Console.WriteLine("RigidBRO deterministic checks passed.");
            return;
        }

        ApplicationConfiguration.Initialize();
        if (args.Length == 0 || args.SequenceEqual(["--rigid"]))
        {
            using var rigid = new RigidCharacterHost();
            rigid.Run();
            return;
        }

        if (args.SequenceEqual(["--prototype"]))
        {
            using var prototype = new NoodleEditorHost();
            prototype.Run();
            return;
        }

        if (args.SequenceEqual(["--bones"]))
        {
            using var editor = new BoneEditorHost();
            editor.Run();
            return;
        }

        MessageBox.Show(
            "Use --rigid, --bones, --prototype, or --check.",
            "App2d.Noodle",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }
}
