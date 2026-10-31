using App2d.Core.Assets;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Textures;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;

namespace App2d.CharacterStudio.CharacterSeeds.Diagnostics;

/// <summary>Renders real game assets and the tile palette without opening a play/edit session.</summary>
internal static class RenderingSmoke2D
{
    public static void Run(string outputDirectory, bool facesOnly = false, bool vegetationOnly = false, bool contactOnly = false, bool timingOnly = false, bool platformOnly = false, IReadOnlyList<string>? tilesetPairs = null, bool spellsOnly = false, bool giantTreesOnly = false, bool editorOnly = false, bool objectsOnly = false)
    {
        Directory.CreateDirectory(outputDirectory);
        using var window = new Form { ClientSize = new Size(1280, 720) };
        using var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef,
            new PresentationParameters
            {
                DeviceWindowHandle = window.Handle,
                BackBufferWidth = 1280,
                BackBufferHeight = 720,
                DepthStencilFormat = DepthFormat.Depth24,
                IsFullScreen = false,
                PresentationInterval = PresentInterval.Immediate
            });
        if (objectsOnly)
        {
            ObjectRenderingSmoke2D.Run(device, outputDirectory);
            return;
        }
        if (facesOnly)
        {
            FaceRenderingSmoke2D.Run(device, outputDirectory);
            return;
        }
        if (giantTreesOnly)
        {
            using var textures = new TextureCache2D(AssetPaths.Current.Runtime);
            GiantTreeStudy2D.Run(device, textures, outputDirectory);
            return;
        }
        if (spellsOnly)
        {
            using var textures = new TextureCache2D(AssetPaths.Current.Runtime);
            SpellRenderingSmoke2D.Run(device, textures, outputDirectory);
            return;
        }
        if (tilesetPairs is not null)
        {
            using var textures = new TextureCache2D(AssetPaths.Current.Runtime);
            if (tilesetPairs is ["level"])
            {
                TilesetRenderingSmoke2D.RunLevel(device, textures, outputDirectory);
            }

            for (var i = 0; i + 1 < tilesetPairs.Count; i += 2)
            {
                TilesetRenderingSmoke2D.Run(device, textures, outputDirectory, tilesetPairs[i], tilesetPairs[i + 1]);
            }

            return;
        }
        if (platformOnly)
        {
            using var textures = new TextureCache2D(AssetPaths.Current.Runtime);
            PlatformStudy2D.Run(device, textures, outputDirectory);
            return;
        }
        if (contactOnly || timingOnly)
        {
            using var textures = new TextureCache2D(AssetPaths.Current.Runtime);
            CombatContactStudy2D.Run(device, textures, outputDirectory, timingOnly);
            return;
        }
        if (vegetationOnly)
        {
            using var textures = new TextureCache2D(AssetPaths.Current.Runtime);
            VegetationRenderingSmoke2D.Run(device, textures, outputDirectory);
            Console.WriteLine("Vegetation rendering smoke checks completed.");
            return;
        }
        using var game = new SideScrollerGame();
        game.Initialize();
        if (editorOnly)
        {
            MapEditorUiSmoke2D.Run(device, game, outputDirectory);
            return;
        }
        using var renderer = new Renderer2D(game.Camera, device);

        foreach (var (width, height) in new[] { (1280, 720), (960, 640) })
        {
            window.ClientSize = new Size(width, height);
            device.Reset(new PresentationParameters
            {
                DeviceWindowHandle = window.Handle,
                BackBufferWidth = width,
                BackBufferHeight = height,
                DepthStencilFormat = DepthFormat.Depth24,
                IsFullScreen = false,
                MultiSampleCount = 4,
                PresentationInterval = PresentInterval.Immediate
            });
            renderer.BeginFrame(width, height, default);
            game.RenderFrame(renderer, default);
            renderer.EndFrame();
            device.Present();

            using var target = RenderCapture2D.CreateTarget(device, width, height);
            using (device.UseRenderTarget(target))
            {
                renderer.BeginFrame(width, height, default);
                game.RenderFrame(renderer, default);
                renderer.EndFrame();
            }
            target.SavePng(Path.Combine(outputDirectory, $"game-{width}x{height}.png"));
        }
        MapEditorUiSmoke2D.Run(device, game, outputDirectory);
        GunRenderingSmoke2D.Run(device, game.Textures, outputDirectory);
        EnemyRenderingSmoke2D.Run(device, game.Textures, outputDirectory);
        AuthoredRenderingSmoke2D.Run(device, game.Textures, outputDirectory);
        VegetationRenderingSmoke2D.Run(device, game.Textures, outputDirectory);
        Console.WriteLine($"MonoGame rendering smoke checks completed: {Path.GetFullPath(outputDirectory)}");
    }
}
