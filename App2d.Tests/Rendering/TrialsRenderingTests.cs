using App2d.Core.Geometry.Primitives;
using App2d.Rendering;
using App2d.Trials;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class TrialsRenderingTests
{
    [Fact]
    public void StartupFrameDrawsTheCircleAndGroundThroughTheGameCamera()
    {
        using var game = new TrialsGame2D();
        game.Initialize();
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(game.Camera, graphics.Device);
        renderer.BeginFrame(128, 128, default);
        game.RenderFrame(renderer, default);
        renderer.EndFrame();
        var pixels = graphics.ReadPixels();
        var player = game.Camera.WorldToDevice(game.Player.WorldObject.Transform.Position);
        var floor = game.Camera.WorldToDevice(Vector2.Zero);

        Assert.Equal(new XnaColor(255, 197, 102), pixels[(int)player.Y * 128 + (int)player.X]);
        Assert.Equal(new XnaColor(117, 193, 150), pixels[(int)MathF.Round(floor.Y) * 128 + 10]);
        Assert.Equal(new XnaColor(18, 23, 28), pixels[110 * 128 + 10]);
    }
}
