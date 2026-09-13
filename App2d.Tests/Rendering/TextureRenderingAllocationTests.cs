using App2d.Core.Geometry;
using App2d.Rendering;
using App2d.Rendering.Textures;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Texture2D = App2d.Rendering.Textures.Texture2D;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class TextureRenderingAllocationTests
{
    [Fact]
    public void WarmTextureDrawDoesNotAllocateManagedMemory()
    {
        using var texture = Texture2D.Load(TestAssets.GetPath("Runtime", "ui", "hud", "weapons", "sword.png"));
        var worldObject = new WorldObject2D(Rectangle2D.FromSize(new Vector2(64f, 64f)),
            new SpriteShader2D(texture, TextureFilter.Point));
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        // Prime MonoGame's dynamic vertex buffer at the measured batch size as well as the texture.
        renderer.BeginFrame(128, 128, default);
        for (var iteration = 0; iteration < 1_000; iteration++) renderer.Draw(worldObject);
        renderer.EndFrame();

        renderer.BeginFrame(128, 128, default);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++) renderer.Draw(worldObject);
        renderer.EndFrame();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }
}
