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
        SaveDetail("trials-suspension-detail.png");
        game.Bike.Frame.AddImpulse(new Vector2(0f, -150f * game.Bike.Frame.Mass));
        var maximumCompression = game.Bike.FrontJoint.Translation;
        for (var step = 0; step < 120; step++)
        {
            Advance(1, 0f, false);
            if (game.Bike.FrontJoint.Translation > maximumCompression)
            {
                maximumCompression = game.Bike.FrontJoint.Translation;
                SaveDetail("trials-compressed.png");
            }
        }
        Advance(120, 0f, false);
        Advance(240, 1f, false);
        Save("trials-drive.png");
        Advance(360, 0f, true);
        Save("trials-brake.png");
        game.Bike.Reset(new(8_000f, game.Terrain.HeightAt(8_000f) + 180f));
        Advance(360, 0f, true);
        Save("trials-travel.png");
        game.Camera.Zoom = 0.5f;
        Save("trials-zoomed-out.png");
        game.Bike.Reset(new(2200f, game.Terrain.HeightAt(2200f) + 180f));
        Advance(360, 0f, true);
        game.Camera.Zoom = 0.8f;
        Save("trials-ramp.png");
        game.Camera.Zoom = 0.3f;
        game.Camera.Position = new(3500f, 180f);
        Save("trials-course.png");

        void SaveDetail(string name)
        {
            var previousPosition = game.Camera.Position;
            var previousZoom = game.Camera.Zoom;
            var x = game.Bike.Frame.WorldObject.Transform.Position.X;
            game.Camera.Position = new(x, game.Terrain.HeightAt(x) + 70f);
            game.Camera.Zoom = 3f;
            Save(name);
            game.Camera.Position = previousPosition;
            game.Camera.Zoom = previousZoom;
        }

        void Advance(int steps, float throttle, bool brake)
        {
            for (var step = 0; step < steps; step++)
            {
                game.Bike.Drive(throttle, 0f, brake);
                game.Update(time, input);
                game.AdvancePresentation(time);
            }
        }

        void Save(string name) => target.SavePng(Path.Combine(directory, name), Microsoft.Xna.Framework.Color.Black, () =>
        {
            renderer.BeginFrame(1280, 720, time);
            game.RenderFrame(renderer, time);
            renderer.EndFrame();
        });
    }
}
