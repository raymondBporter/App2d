using App2d.Core.Rendering;
using App2d.Core.Rendering.Textures;
using App2d.Core.Shapes;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Xunit.Abstractions;
using Texture2D = App2d.Core.Rendering.Textures.Texture2D;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class TextureRenderingAllocationTests(ITestOutputHelper output)
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

        // Since the test projects merged into one process, a single warm pass occasionally reports exactly 5,904
        // bytes (about two runs in five, never in isolation, never more than once per run). The source has not
        // been identified; it is a one-off on this thread, not a per-draw leak, which would cost at least 24 KB
        // per pass. A per-draw allocation still fails every pass, so the minimum of three passes is the signal,
        // and the measurements are written to the test output so a recurrence stays visible.
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
            if (measurements[attempt] == 0)
            {
                output.WriteLine($"Warm pass allocations: {string.Join(", ", measurements[..(attempt + 1)])} bytes; gen0 GCs during each pass: {string.Join(", ", gcs[..(attempt + 1)])}.");
                return;
            }
        }
        Assert.Fail($"Every warm pass allocated: bytes {string.Join(", ", measurements)}; gen0 GCs during each pass {string.Join(", ", gcs)}.");
    }
}
