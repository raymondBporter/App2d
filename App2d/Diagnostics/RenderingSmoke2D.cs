using App2d.Editor;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;

namespace App2d.Diagnostics;

/// <summary>Renders real game assets and the tile palette without opening a play/edit session.</summary>
internal static class RenderingSmoke2D
{
    public static void Run(string outputDirectory, bool facesOnly = false, bool vegetationOnly = false, bool contactOnly = false, bool timingOnly = false, bool platformOnly = false)
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
        if (facesOnly) { FaceRenderingSmoke2D.Run(device, outputDirectory); return; }
        if (platformOnly)
        {
            using var textures = new App2d.Rendering.Textures.TextureCache2D(AssetPaths.Root);
            PlatformStudy2D.Run(device, textures, outputDirectory);
            return;
        }
        if (contactOnly || timingOnly)
        {
            using var textures = new App2d.Rendering.Textures.TextureCache2D(AssetPaths.Root);
            CombatContactStudy2D.Run(device, textures, outputDirectory, timingOnly);
            return;
        }
        if (vegetationOnly)
        {
            using var textures = new Rendering.Textures.TextureCache2D(AssetPaths.Root);
            VegetationRenderingSmoke2D.Run(device, textures, outputDirectory);
            Console.WriteLine("Vegetation rendering smoke checks completed.");
            return;
        }
        using var game = new SideScrollerGame();
        game.Initialize();
        using var renderer = new Renderer2D(game.Camera, device);
        var loaded = LevelBootstrap2D.Load();
        using var editor = new TileEditor2D(loaded.TileMap,
            () => throw new InvalidOperationException("Rendering smoke checks never open a level for writing."),
            game.Camera, Vector2.Zero, 32f);

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

            using var target = new RenderTarget2D(device, width, height, false, SurfaceFormat.Color, DepthFormat.Depth24, 4, RenderTargetUsage.DiscardContents);
            device.SetRenderTarget(target);
            renderer.BeginFrame(width, height, default);
            game.RenderFrame(renderer, default);
            renderer.EndFrame();
            Save(target, $"game-{width}x{height}.png");

            device.SetRenderTarget(target);
            renderer.BeginFrame(width, height, default);
            game.RenderFrame(renderer, default);
            TileEditorMenu2D.Draw(renderer, editor, game.Textures);
            renderer.EndFrame();
            Save(target, $"editor-{width}x{height}.png");
        }
        GunRenderingSmoke2D.Run(device, game.Textures, outputDirectory);
        EnemyRenderingSmoke2D.Run(device, game.Textures, outputDirectory);
        AuthoredRenderingSmoke2D.Run(device, game.Textures, outputDirectory);
        VegetationRenderingSmoke2D.Run(device, game.Textures, outputDirectory);
        Console.WriteLine($"MonoGame rendering smoke checks completed: {Path.GetFullPath(outputDirectory)}");

        void Save(RenderTarget2D target, string name)
        {
            device.SetRenderTarget(null);
            using var stream = File.Create(Path.Combine(outputDirectory, name));
            target.SaveAsPng(stream, target.Width, target.Height);
        }
    }
}
