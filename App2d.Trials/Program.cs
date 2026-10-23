using App2d.Core.Hosting;
using App2d.Trials;

ApplicationConfiguration.Initialize();
if (args is ["--render-smoke", var directory])
{
    TrialsRenderingSmoke2D.Run(directory);
    return;
}

using var host = new GameHost(new TrialsGame2D());
host.Run();
