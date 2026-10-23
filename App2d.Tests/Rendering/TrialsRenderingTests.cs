using App2d.Rendering;
using App2d.Trials;
using System.Numerics;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class TrialsRenderingTests
{
    [Fact]
    public void StartupFrameDrawsBothWheelHubsAndGroundThroughTheGameCamera()
    {
        using var game = new TrialsGame2D();
        game.Initialize();
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(game.Camera, graphics.Device);
        renderer.BeginFrame(128, 128, default);
        game.RenderFrame(renderer, default);
        renderer.EndFrame();
        var pixels = graphics.ReadPixels();
        var floor = game.Camera.WorldToDevice(Vector2.Zero);

        foreach (var wheel in new[] { game.Bike.RearWheel, game.Bike.FrontWheel })
        {
            var hub = game.Camera.WorldToDevice(wheel.WorldCenterOfMass);
            Assert.Equal(new Color4b(255, 197, 102), pixels[(int)MathF.Round(hub.Y) * 128 + (int)MathF.Round(hub.X)]);
        }
        Assert.Equal(new Color4b(117, 193, 150), pixels[(int)MathF.Round(floor.Y) * 128 + 10]);
        Assert.Equal(new Color4b(18, 23, 28), pixels[110 * 128 + 10]);
    }
}
