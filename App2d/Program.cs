using App2d;

if (args.SequenceEqual(["--migrate-level"]))
{
    using var database = LevelBootstrap2D.OpenForEditing();
    return;
}

ApplicationConfiguration.Initialize();

if (args is ["--hitstop-study", var timingDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(timingDirectory, timingOnly: true);
    return;
}

if (args is ["--contact-study", var contactDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(contactDirectory, contactOnly: true);
    return;
}

if (args is ["--face-smoke", var faceDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(faceDirectory, facesOnly: true);
    return;
}

if (args is ["--render-smoke", var outputDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(outputDirectory);
    return;
}

if (args is ["--vegetation-smoke", var vegetationDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(vegetationDirectory, vegetationOnly: true);
    return;
}

using var host = new GameHost(new SideScrollerGame());
host.Run();
