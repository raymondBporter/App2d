using System.Numerics;
using App2d.Engine;
using App2d.Engine.Geometry;
using App2d.Engine.Rendering;
using App2d.Engine.Rendering.Textures;
using SkiaSharp;

namespace App2d.Tests.Rendering;

public sealed class TextureRenderingAllocationTests
{
    private static readonly string TexturePath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "Assets",
        "Content",
        "ui",
        "hud",
        "weapons",
        "sword.png"));

    [Fact]
    public void WarmTextureDrawDoesNotAllocateManagedMemory()
    {
        using var texture = Texture2D.Load(TexturePath);
        var shader = new SpriteShader2D(texture, SKFilterMode.Nearest);
        var worldObject = new WorldObject2D(
            Rectangle2D.FromSize(new Vector2(64f, 64f)),
            shader);
        using var bitmap = new SKBitmap(new SKImageInfo(
            128,
            128,
            SKColorType.Bgra8888,
            SKAlphaType.Unpremul));
        using var canvas = new SKCanvas(bitmap);
        using var renderer = new Renderer2D(new Camera2D());
        renderer.BeginFrame(canvas, bitmap.Width, bitmap.Height, default);

        for (var iteration = 0; iteration < 100; iteration++)
            renderer.Draw(worldObject);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
            renderer.Draw(worldObject);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
}
