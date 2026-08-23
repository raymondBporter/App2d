using App2d;
using App2d.Engine;

ApplicationConfiguration.Initialize();

using var host = new GameHost(new SideScrollerGame());
host.Run();
