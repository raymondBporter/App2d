using App2d.Core;
using App2d.Core.Hosting;
using App2d.Core.Input;
using App2d.Core.Rendering;
using App2d.Core.Shapes;
using App2d.Gameplay.World.Vegetation;
using App2d.Presentation.World.Vegetation;
using App2d.Rendering;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class ParallaxRenderingTests
{
    [Fact]
    public void LayerUsesItsOwnCullingAndRestoresTheWorldCameraWithoutMovingObjects()
    {
        using var graphics = new GraphicsTestContext();
        var camera = new Camera2D { Position = new(200f, 100f) };
        using var renderer = new Renderer2D(camera, graphics.Device);
        var layer = new ParallaxLayer2D(4f);
        var scenery = Rectangle(12f, 12f, XnaColor.Red);
        scenery.Transform.Position = new(40f, 20f);
        layer.Scene.Add(scenery);
        var world = Rectangle(12f, 12f, XnaColor.Lime);
        world.Transform.Position = new(230f, 100f);
        var originalTransform = scenery.Transform.Matrix;

        var first = Render();
        Assert.Equal(XnaColor.Red, At(first, 64, 64));
        Assert.Equal(XnaColor.Lime, At(first, 94, 64));
        camera.Position += new Vector2(40f, 20f);
        var moved = Render();
        Assert.Equal(XnaColor.Red, At(moved, 56, 68)); // One fifth of both camera displacements.
        Assert.Equal(XnaColor.Lime, At(moved, 54, 84));
        Assert.Equal(new Vector2(240f, 120f), camera.Position);
        Assert.Equal(originalTransform, scenery.Transform.Matrix);

        XnaColor[] Render()
        {
            renderer.BeginFrame(128, 128, default);
            renderer.Clear(XnaColor.Transparent);
            renderer.Draw(layer);
            renderer.Draw(world);
            renderer.EndFrame();
            return graphics.ReadPixels();
        }
    }

    [Theory]
    [InlineData(-129.5f, 0.5f, 0f)]
    [InlineData(63.5f, 1f, 0f)]
    [InlineData(10_000f, 0.5f, 0.3f)]
    public void RepeatingLayerCoversSeamsAndEveryVisibleCopyWhenZoomedOut(float x, float zoom, float rotation)
    {
        using var graphics = new GraphicsTestContext();
        var camera = new Camera2D { Position = new(x, 0f), Zoom = zoom, Rotation = rotation };
        using var renderer = new Renderer2D(camera, graphics.Device);
        var layer = new ParallaxLayer2D(1f) { RepeatWidth = 64f };
        layer.Scene.Add(Rectangle(64f, 512f, XnaColor.White));
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(XnaColor.Transparent);
        renderer.Draw(layer);
        renderer.EndFrame();
        Assert.All(graphics.ReadPixels(), pixel => Assert.Equal(XnaColor.White, pixel));
    }

    [Fact]
    public void DepthOrdersLayersBeforeTheirObjectZIndicesAndCanChangeAtRuntime()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        var near = new ParallaxLayer2D(1f);
        near.Scene.Add(Rectangle(256f, 256f, XnaColor.Red));
        var far = new ParallaxLayer2D(4f);
        var distantVisual = Rectangle(256f, 256f, XnaColor.Blue);
        distantVisual.ZIndex = int.MaxValue;
        far.Scene.Add(distantVisual);
        ParallaxBackground2D background = [near, far];
        Assert.Equal(XnaColor.Red, RenderCenter());
        near.Depth = 8f;
        Assert.Equal(XnaColor.Blue, RenderCenter());

        XnaColor RenderCenter()
        {
            renderer.BeginFrame(128, 128, default);
            renderer.Clear(XnaColor.Transparent);
            renderer.Draw(background);
            renderer.EndFrame();
            return At(graphics.ReadPixels(), 64, 64);
        }
    }

    [Fact]
    public void ARejectedRepeatedShapeStillRestoresTheWorldCamera()
    {
        using var graphics = new GraphicsTestContext();
        var camera = new Camera2D { Position = new(200f, 100f) };
        using var renderer = new Renderer2D(camera, graphics.Device);
        var layer = new ParallaxLayer2D(4f) { RepeatWidth = 64f };
        layer.Scene.Add(new WorldObject2D(HalfSpace2D.Builder.Create(Vector2.UnitY, 0f),
            new SolidColorShader(XnaColor.Red)));
        var world = Rectangle(12f, 12f, XnaColor.Lime);
        world.Transform.Position = camera.Position;
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(XnaColor.Transparent);
        Assert.Throws<InvalidOperationException>(() => renderer.Draw(layer));
        renderer.Draw(world);
        renderer.EndFrame();
        Assert.Equal(XnaColor.Lime, At(graphics.ReadPixels(), 64, 64));
    }

    [Fact]
    public void BackgroundAlwaysDrawsBehindTheActualGiantTreeAndFollowsTheGraphicsToggle()
    {
        using var graphics = new GraphicsTestContext();
        using var game = new TreeGame();
        using var renderer = new Renderer2D(game.Camera, graphics.Device);
        var withoutBackground = Render();
        var layer = new ParallaxLayer2D(4f);
        var distantVisual = Rectangle(512f, 512f, XnaColor.Magenta);
        distantVisual.ZIndex = int.MaxValue;
        layer.Scene.Add(distantVisual);
        game.Background.Add(layer);
        var withBackground = Render();
        Assert.Equal(At(withoutBackground, 64, 64), At(withBackground, 64, 64));
        Assert.NotEqual(XnaColor.Magenta, At(withBackground, 64, 64));
        Assert.Equal(XnaColor.Magenta, At(withBackground, 0, 127));
        game.DrawGraphics = false;
        Assert.Equal(new XnaColor(24, 27, 36), At(Render(), 64, 64));

        XnaColor[] Render()
        {
            renderer.BeginFrame(128, 128, default);
            game.RenderFrame(renderer, default);
            renderer.EndFrame();
            return graphics.ReadPixels();
        }
    }

    private static WorldObject2D Rectangle(float width, float height, XnaColor color) =>
        new(Rectangle2D.Builder.CreateFromSize(width, height), new SolidColorShader(color));

    private static XnaColor At(XnaColor[] pixels, int x, int y) => pixels[y * 128 + x];

    private sealed class TreeGame : Game2D
    {
        private readonly GiantTreeVisual2D _tree = new(new GiantTree2D(Vector2.Zero,
            new GiantTreeSettings2D { Height = 1000f, TrunkWidth = 60f, BranchGap = new(200f, 200f) }, 42));

        public TreeGame() => Camera.Position = new(0f, 100f);

        public override void Update(FrameTime time, InputState input) { }

        public override void Render(Renderer2D renderer)
        {
            _tree.Render(renderer, Camera.VisibleWorldBounds, default(VegetationWind2D));
            renderer.Draw(Scene, int.MinValue, 0);
        }
    }
}
