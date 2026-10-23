using App2d.Core;
using App2d.Core.Input;
using App2d.Core.Rendering;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;

namespace App2d.Beamwright;

internal static class BeamwrightRenderingSmoke
{
    public static void Run(string directory)
    {
        Directory.CreateDirectory(directory);
        using var window = new Form { ClientSize = new Size(1440, 900) };
        using var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef, new PresentationParameters
        {
            DeviceWindowHandle = window.Handle,
            BackBufferWidth = 1440,
            BackBufferHeight = 900,
            DepthStencilFormat = DepthFormat.None,
            PresentationInterval = PresentInterval.Immediate
        });
        using var game = new BeamwrightGame2D();
        using var renderer = new Renderer2D(game.Camera, device);
        var time = new FrameTime(1f / 120f, .8, 1);
        var input = new InputState();
        foreach (var (index, name) in new[] { (0, "first-light"), (7, "spectrum"), (25, "aperture"), (32, "fiber-exchange"), (35, "grandmaster"), (-1, "free-bench") })
        {
            game.Session.LoadLevel(index);
            if (index >= 0)
            {
                foreach (var part in game.Session.Level.Solution)
                    if (!game.Session.TryPlace(part.Type, part.X, part.Y, part.Angle)) throw new InvalidOperationException("Cannot place the recorded solution.");
            }

            game.Camera.SetViewport(1440, 900);
            game.Update(time, input);
            using var target = RenderCapture2D.CreateTarget(device, 1440, 900, DepthFormat.None);
            target.SavePng(Path.Combine(directory, name + ".png"), Color4b.Black, () =>
            {
                renderer.BeginFrame(1440, 900, time);
                game.RenderFrame(renderer, time);
                renderer.EndFrame();
            });
        }
        game.Session.LoadLevel(0);
        game.Camera.SetViewport(960, 640);
        game.Update(time, input);
        SaveSmall("desktop-960.png");
        game.OpenLevelPicker();
        SaveSmall("level-picker-960.png");

        void SaveSmall(string name)
        {
            using var target = RenderCapture2D.CreateTarget(device, 960, 640, DepthFormat.None);
            target.SavePng(Path.Combine(directory, name), Color4b.Black, () =>
            {
                renderer.BeginFrame(960, 640, time);
                game.RenderFrame(renderer, time);
                renderer.EndFrame();
            });
        }
    }
}
