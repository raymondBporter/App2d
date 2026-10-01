using App2d;

if (args.SequenceEqual(["--migrate-level"]))
{
    using var database = LevelBootstrap2D.OpenForEditing();
    return;
}

ApplicationConfiguration.Initialize();

if (args is ["--spell-study", var spellDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(spellDirectory, spellsOnly: true);
    return;
}

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

if (args is ["--platform-study", var platformDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(platformDirectory, platformOnly: true);
    return;
}

if (args is ["--render-smoke", var outputDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(outputDirectory);
    return;
}

if (args is ["--tileset-smoke", var tilesetDirectory, .. var tilesetPairs] &&
    (tilesetPairs is ["level"] || tilesetPairs.Length is > 0 and var count && count % 2 == 0))
{
    App2d.Diagnostics.RenderingSmoke2D.Run(tilesetDirectory, tilesetPairs: tilesetPairs);
    return;
}

if (args is ["--vegetation-smoke", var vegetationDirectory])
{
    App2d.Diagnostics.RenderingSmoke2D.Run(vegetationDirectory, vegetationOnly: true);
    return;
}

using var host = new GameHost(new SideScrollerGame());
host.Run();
