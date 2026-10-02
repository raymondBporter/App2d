using App2d.Core.Shapes;
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

        // Other tests share this process and can trigger GCs or tiering work while this thread measures, and
        // a one-off allocation from that (a few KB, observed once per run at most) is not a renderer leak.
        // A per-draw allocation would show in every pass, so the minimum of three passes is the signal.
        var measurements = new long[3];
        var gcs = new int[3];
        for (var attempt = 0; attempt < measurements.Length; attempt++)
        {
            renderer.BeginFrame(128, 128, default);
            var gen0 = GC.CollectionCount(0);
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var iteration = 0; iteration < 1_000; iteration++) renderer.Draw(worldObject);
            renderer.EndFrame();
            measurements[attempt] = GC.GetAllocatedBytesForCurrentThread() - before;
            gcs[attempt] = GC.CollectionCount(0) - gen0;
            if (measurements[attempt] == 0) return;
        }
        Assert.Fail($"Every warm pass allocated: bytes {string.Join(", ", measurements)}; gen0 GCs during each pass {string.Join(", ", gcs)}.");
    }
}
