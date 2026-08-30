using App2d;

if (args.SequenceEqual(["--migrate-level"]))
{
    using var database = LevelBootstrap2D.OpenForEditing();
    return;
}

ApplicationConfiguration.Initialize();

using var host = new GameHost(new SideScrollerGame());
host.Run();
