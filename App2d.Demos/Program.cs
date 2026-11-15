namespace App2d.Demos;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var game = new DemoGame();
        game.Run();
    }
}
