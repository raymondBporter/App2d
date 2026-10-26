using App2d.Beamwright.Optics;
using App2d.Core.Hosting;

namespace App2d.Beamwright;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args is ["--verify"]) return CampaignVerification.Run(Console.Out);
        ApplicationConfiguration.Initialize();
        if (args is ["--render-smoke", var directory])
        {
            BeamwrightRenderingSmoke.Run(directory);
            return 0;
        }
        var savePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "App2d", "Beamwright", "progress.json");
        using var host = new GameHost(new BeamwrightGame2D(new(savePath: savePath)));
        host.Run();
        return 0;
    }
}
