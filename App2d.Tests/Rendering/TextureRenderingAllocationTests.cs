using App2d.Core.Rendering;
using App2d.Core.Rendering.Textures;
using App2d.Core.Shapes;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Xunit.Abstractions;
using Texture2D = App2d.Core.Rendering.Textures.Texture2D;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class TextureRenderingAllocationTests(ITestOutputHelper output)
{
    [Fact]
    public void WarmRetainedMeshDrawsDoNotAllocateManagedMemory()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        using var characters = new Core.Rendering.Characters.PointCharacterRenderer(graphics.Device);
        var mesh = new Core.Geometry.Meshes.TriangleMesh2D(
            [new(-8, -8), new(8, -8), new(0, 8)], [0, 1, 2]);
        var character = new Core.Rendering.Characters.CharacterMesh(3);
        character.Triangle(new(-8, -8, 0), new(8, -8, 0), new(0, 8, 0), Color4b.White);
        var material = new SolidColorShader(Color4b.White);
        var projection = Core.Rendering.Characters.PointCharacterRenderer.Projection(128, 128, new(64), 1);
        void Draw()
        {
            for (var iteration = 0; iteration < 300; iteration++)
            {
                renderer.DrawMesh(mesh, material);
                characters.Draw(character, projection, Microsoft.Xna.Framework.Matrix.Identity, writeDepth: false);
            }
        }
        renderer.BeginFrame(128, 128, default);
        Draw();
        renderer.EndFrame();
        var measurements = new long[3];
        for (var attempt = 0; attempt < measurements.Length; attempt++)
        {
            renderer.BeginFrame(128, 128, default);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Draw();
            renderer.EndFrame();
            measurements[attempt] = GC.GetAllocatedBytesForCurrentThread() - before;
        }
        output.WriteLine($"Retained mesh draw allocations: {string.Join(", ", measurements)} bytes.");
        Assert.Equal(0, measurements.Min());
        Assert.Equal(1, renderer.MeshUploadCount);
        Assert.Equal(1, characters.MeshUploadCount);
    }

    [Fact]
    public void WarmTextureDrawDoesNotAllocateManagedMemory()
    {
        using var texture = Texture2D.Load(TestAssets.GetPath("Runtime", "ui", "hud", "weapons", "sword.png"));
        var worldObject = new WorldObject2D(Rectangle2D.Builder.CreateFromSize(64f, 64f),
            new SpriteShader2D(texture, TextureFilter.Point));
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        // Prime MonoGame's dynamic vertex buffer at the measured batch size as well as the texture.
        renderer.BeginFrame(128, 128, default);
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            renderer.Draw(worldObject);
        }

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
            for (var iteration = 0; iteration < 1_000; iteration++)
            {
                renderer.Draw(worldObject);
            }

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
