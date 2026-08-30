using App2d;

ApplicationConfiguration.Initialize();

using var host = new GameHost(new SideScrollerGame());
host.Run();
