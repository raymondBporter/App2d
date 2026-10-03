using App2d.Core;
using App2d.Core.Hosting;
using App2d.Core.Input;
using App2d.Core.Physics;
using App2d.Core.Rendering;
using App2d.Core.Shapes;
using App2d.Rendering;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class GameRenderPassTests
{
    [Fact]
    public void GameAttachesOnlyOnePhysicsWorld()
    {
        using var game = new TestGame();
        var world = new PhysicsWorld2D();
        game.AttachWorld(world);
        game.AttachWorld(world);

        Assert.Same(world, game.PhysicsWorld);
        Assert.Throws<InvalidOperationException>(() => game.AttachWorld(new PhysicsWorld2D()));
        Assert.Same(world, game.PhysicsWorld);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DebugGridOverlaysGameAndStaysBelowUiEvenWhenGameGraphicsAreHidden(bool drawGraphics)
    {
        using var graphics = new GraphicsTestContext();
        using var game = new TestGame();
        using var renderer = new Renderer2D(game.Camera, graphics.Device);
        game.DeveloperConsole.Execute($"draw_graphics {drawGraphics}");
        var background = drawGraphics ? XnaColor.Red : new XnaColor(24, 27, 36);

        // The grid starts disabled, and its toggle is independent of the graphics toggle.
        var withoutGrid = Render();
        Assert.Equal(background, At(withoutGrid, 64, 80));
        game.DeveloperConsole.Execute("draw_grid true");
        var withGrid = Render();
        Assert.True(At(withGrid, 64, 80).G > background.G);
        Assert.Equal(background, At(withGrid, 80, 80));
        Assert.Equal(XnaColor.Blue, At(withGrid, 64, 16)); // UI covers the grid's vertical axis.
        Assert.Equal(drawGraphics ? new[] { "game", "debug", "ui" } : ["debug", "ui"], game.Passes);

        // World diagnostics use the game camera; screen UI does not move with it.
        game.Camera.Position = new Vector2(13, 9);
        var moved = Render();
        Assert.True(At(moved, 51, 80).G > background.G);
        Assert.Equal(background, At(moved, 64, 80));
        Assert.Equal(XnaColor.Blue, At(moved, 64, 16));

        game.DeveloperConsole.Execute("toggle draw_grid");
        Assert.Equal(background, At(Render(), 51, 80));

        XnaColor[] Render()
        {
            game.Passes.Clear();
            renderer.BeginFrame(128, 128, default);
            game.RenderFrame(renderer, default);
            renderer.EndFrame();
            return graphics.ReadPixels();
        }
    }

    private static XnaColor At(XnaColor[] pixels, int x, int y) => pixels[y * 128 + x];

    private sealed class TestGame : Game2D
    {
        public List<string> Passes { get; } = [];

        public void AttachWorld(PhysicsWorld2D world) => AttachPhysicsWorld(world);

        public TestGame() => Scene.Add(new WorldObject2D(
            Rectangle2D.FromSize(new Vector2(512)), new SolidColorShader(XnaColor.Red)));

        public override void Update(FrameTime time, InputState input) { }

        public override void Render(Renderer2D renderer)
        {
            Passes.Add("game");
            base.Render(renderer);
        }

        public override void RenderWorldDebug(Renderer2D renderer)
        {
            Passes.Add("debug");
            base.RenderWorldDebug(renderer);
        }

        public override void RenderUI(Renderer2D renderer, FrameTime time)
        {
            Passes.Add("ui");
            renderer.DrawScreenRoundedRectangle(new(56, 0, 72, 32), 0, XnaColor.Blue);
            base.RenderUI(renderer, time);
        }
    }
}
