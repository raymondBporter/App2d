using App2d.Core.Rendering.Characters;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class CharacterBufferRenderingTests
{
    private static readonly Matrix Projection = PointCharacterRenderer.Projection(128, 128, new(64), 1);

    private static void AddTriangle(CharacterMesh mesh, Color color) =>
        mesh.Triangle(new(-12, -12, 0), new(12, -12, 0), new(0, 12, 0), color);

    [Fact]
    public void MaterialRangesMatricesAndOtherMeshesReuseUnchangedBuffers()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new PointCharacterRenderer(graphics.Device);
        var drawing = new PuppetDrawing();
        AddTriangle(drawing.Mesh, Color.Red);
        AddTriangle(drawing.Mesh, Color.Blue);
        drawing.Batches.Add(new(0, 3, WriteDepth: false));
        drawing.Batches.Add(new(3, 3, Blend: "additive", WriteDepth: false));
        graphics.Device.Clear(Color.Transparent);
        renderer.Draw(drawing, Projection, Matrix.CreateTranslation(-24, 0, 0));
        renderer.Draw(drawing, Projection, Matrix.CreateTranslation(24, 0, 0));
        Assert.Equal(1, renderer.MeshUploadCount);
        Assert.Equal(4, renderer.MeshDrawCount);
        var pixels = graphics.ReadPixels();
        Assert.Equal(Color.Magenta, pixels[64 * 128 + 40]);
        Assert.Equal(Color.Magenta, pixels[64 * 128 + 88]);

        var other = new CharacterMesh(3);
        AddTriangle(other, Color.Lime);
        renderer.Draw(other, Projection, Matrix.Identity, writeDepth: false);
        renderer.Draw(drawing, Projection, Matrix.Identity);
        Assert.Equal(2, renderer.MeshUploadCount);
    }

    [Fact]
    public void RebuildWithSameCountAndBufferGrowthUploadNewDataWithoutChangingEarlierDraws()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new PointCharacterRenderer(graphics.Device);
        var mesh = new CharacterMesh(3);
        AddTriangle(mesh, Color.Red);
        var version = mesh.Version;
        graphics.Device.Clear(Color.Transparent);
        renderer.Draw(mesh, Projection, Matrix.CreateTranslation(-32, 0, 0), writeDepth: false);
        mesh.Clear();
        AddTriangle(mesh, Color.Lime);
        Assert.True(mesh.Version > version);
        renderer.Draw(mesh, Projection, Matrix.Identity, writeDepth: false);
        AddTriangle(mesh, Color.Blue);
        renderer.Draw(mesh, Projection, Matrix.CreateTranslation(32, 0, 0), writeDepth: false);
        Assert.Equal(3, renderer.MeshUploadCount);
        var pixels = graphics.ReadPixels();
        Assert.Equal(Color.Red, pixels[64 * 128 + 32]);
        Assert.Equal(Color.Lime, pixels[64 * 128 + 64]);
        Assert.Equal(Color.Blue, pixels[64 * 128 + 96]);
    }

    [Fact]
    public void EmptyMeshesAndInvalidDrawRangesDoNotSubmitOrUpload()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new PointCharacterRenderer(graphics.Device);
        var mesh = new CharacterMesh(3);
        renderer.Draw(mesh, Projection, Matrix.Identity);
        Assert.Equal(0, renderer.MeshUploadCount);
        AddTriangle(mesh, Color.Red);
        Assert.Throws<ArgumentOutOfRangeException>(() => renderer.Draw(mesh, Projection, Matrix.Identity, start: -3));
        Assert.Throws<ArgumentOutOfRangeException>(() => renderer.Draw(mesh, Projection, Matrix.Identity, start: 1, count: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => renderer.Draw(mesh, Projection, Matrix.Identity, count: 6));
        renderer.Draw(mesh, Projection, Matrix.Identity, start: 3);
        Assert.Equal(0, renderer.MeshUploadCount);
        mesh.Vertex(Vector3.Zero, Color.Red);
        Assert.Throws<ArgumentOutOfRangeException>(() => renderer.Draw(mesh, Projection, Matrix.Identity));
        Assert.Equal(0, renderer.MeshDrawCount);
    }

    [Fact]
    public void DepthTextureAlphaAndCallerStatesSurviveBufferedDrawing()
    {
        using var graphics = new GraphicsTestContext();
        using var target = new RenderTarget2D(graphics.Device, 128, 128, false, SurfaceFormat.Color, DepthFormat.Depth24);
        graphics.Device.SetRenderTarget(target);
        using var texture = new Texture2D(graphics.Device, 1, 1);
        texture.SetData([new Color(255, 0, 0, 128)]);
        using var renderer = new PointCharacterRenderer(graphics.Device);
        var mesh = new CharacterMesh(3);
        // Near opaque green, then farther textured red: depth read must keep the near triangle.
        mesh.Triangle(new(-12, -12, -1), new(12, -12, -1), new(0, 12, -1), Color.Lime);
        mesh.Triangle(new(-12, -12, 1), new(12, -12, 1), new(0, 12, 1), Color.White);
        graphics.Device.Clear(Color.White);
        graphics.Device.BlendState = BlendState.Opaque;
        graphics.Device.DepthStencilState = DepthStencilState.None;
        graphics.Device.RasterizerState = RasterizerState.CullClockwise;
        graphics.Device.SamplerStates[0] = SamplerState.PointWrap;
        renderer.Draw(mesh, Projection, Matrix.Identity, start: 0, count: 3);
        renderer.Draw(mesh, Projection, Matrix.Identity, texture, writeDepth: false, start: 3);
        renderer.Draw(mesh, Projection, Matrix.CreateTranslation(32, 0, 0), texture,
            writeDepth: false, start: 3);
        Assert.Same(BlendState.Opaque, graphics.Device.BlendState);
        Assert.Same(DepthStencilState.None, graphics.Device.DepthStencilState);
        Assert.Same(RasterizerState.CullClockwise, graphics.Device.RasterizerState);
        Assert.Same(SamplerState.PointWrap, graphics.Device.SamplerStates[0]);
        graphics.Device.SetRenderTarget(null);
        var pixels = new Color[128 * 128];
        target.GetData(pixels);
        Assert.Equal(Color.Lime, pixels[64 * 128 + 64]);
        Assert.Equal(new Color(255, 127, 127, 191), pixels[64 * 128 + 96]);
        Assert.Equal(1, renderer.MeshUploadCount);
    }

    [Fact]
    public void LargeVertexStreamsSubmitTheFinalPartialChunk()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new PointCharacterRenderer(graphics.Device);
        var mesh = new CharacterMesh();
        for (var triangle = 0; triangle < 8192; triangle++)
        {
            mesh.Triangle(new(-40, -12, 0), new(-16, -12, 0), new(-28, 12, 0), Color.Red);
        }
        mesh.Triangle(new(16, -12, 0), new(40, -12, 0), new(28, 12, 0), Color.Blue);
        graphics.Device.Clear(Color.Transparent);
        renderer.Draw(mesh, Projection, Matrix.Identity, writeDepth: false);
        var pixels = graphics.ReadPixels();
        Assert.Equal(Color.Red, pixels[64 * 128 + 36]);
        Assert.Equal(Color.Blue, pixels[64 * 128 + 92]);
        Assert.Equal(2, renderer.MeshDrawCount);
        Assert.Equal(1, renderer.MeshUploadCount);
    }

    [Fact]
    public void EvictionClearAndDisposalReleaseCharacterBuffers()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new PointCharacterRenderer(graphics.Device);
        var mesh = new CharacterMesh(3);
        AddTriangle(mesh, Color.Red);
        renderer.Draw(mesh, Projection, Matrix.Identity);
        Assert.True(renderer.CachedMeshBytes > 0);
        renderer.ClearMeshCache();
        Assert.Equal(0, renderer.CachedMeshBytes);
        renderer.Draw(mesh, Projection, Matrix.Identity);
        Assert.Equal(2, renderer.MeshUploadCount);
        renderer.MeshCacheBudgetBytes = 0;
        renderer.Draw(mesh, Projection, Matrix.Identity);
        Assert.Equal(0, renderer.CachedMeshBytes);
        renderer.Draw(mesh, Projection, Matrix.Identity);
        Assert.Equal(3, renderer.MeshUploadCount);
        renderer.Dispose();
        Assert.Equal(0, renderer.CachedMeshBytes);
        Assert.Throws<ObjectDisposedException>(() => renderer.Draw(mesh, Projection, Matrix.Identity));
    }
}
