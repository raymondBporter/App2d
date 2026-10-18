using App2d.Core;
using App2d.Core.Input;
using App2d.Core.Rendering;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;

namespace App2d.Trials;

internal static class TrialsRenderingSmoke2D
{
    public static void Run(string directory)
    {
        Directory.CreateDirectory(directory);
        using var window = new Form { ClientSize = new Size(1280, 720) };
        using var device = new GraphicsDevice(GraphicsAdapter.DefaultAdapter, GraphicsProfile.HiDef,
            new PresentationParameters
            {
                DeviceWindowHandle = window.Handle,
                BackBufferWidth = 1280,
                BackBufferHeight = 720,
                DepthStencilFormat = DepthFormat.None,
                PresentationInterval = PresentInterval.Immediate
            });
        using var game = new TrialsGame2D();
        game.Initialize();
        var input = new InputState();
        var time = new FrameTime(1f / 120f, 0, 0);
        for (var step = 0; step < 240; step++)
        {
            game.Update(time, input);
            game.AdvancePresentation(time);
        }

        using var renderer = new Renderer2D(game.Camera, device);
        using var target = RenderCapture2D.CreateTarget(device, 1280, 720, DepthFormat.None);
        Save("trials-start.png");
        game.Player.WorldObject.Transform.Position += new Vector2(8_000f, 0f);
        for (var step = 0; step < 240; step++)
        {
            game.AdvancePresentation(time);
        }
        Save("trials-travel.png");
        game.Camera.Zoom = 0.5f;
        Save("trials-zoomed-out.png");

        void Save(string name) => target.SavePng(Path.Combine(directory, name), Microsoft.Xna.Framework.Color.Black, () =>
        {
            renderer.BeginFrame(1280, 720, time);
            game.RenderFrame(renderer, time);
            renderer.EndFrame();
        });
    }
}
