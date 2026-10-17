using App2d.Core.Geometry.Meshes;
using App2d.Core.Geometry.Primitives;
using App2d.Core.Geometry.Operations.Vertices;
using App2d.Core.Rendering;
using App2d.Core.Rendering.Textures;
using App2d.Core.Shapes;
using App2d.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;
using Texture2D = App2d.Core.Rendering.Textures.Texture2D;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class RetainedMeshRenderingTests
{
    private static TriangleMesh2D Quad(float radius = 16) => new(
        [new(-radius, -radius), new(radius, -radius), new(radius, radius), new(-radius, radius)],
        [0, 1, 2, 0, 2, 3]);

    [Fact]
    public void TransformsColorsAndCameraChangesReuseOneUpload()
    {
        using var graphics = new GraphicsTestContext();
        var camera = new Camera2D();
        using var renderer = new Renderer2D(camera, graphics.Device);
        var mesh = Quad(8);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color.Transparent);
        renderer.DrawMesh(mesh, new SolidColorShader(Color.Red), Matrix3x2.CreateTranslation(-24, 0));
        renderer.DrawMesh(mesh, new SolidColorShader(Color.Lime), Matrix3x2.CreateTranslation(24, 0));
        renderer.EndFrame();
        var pixels = graphics.ReadPixels();
        Assert.Equal(Color.Red, pixels[64 * 128 + 40]);
        Assert.Equal(Color.Lime, pixels[64 * 128 + 88]);
        Assert.Equal(1, renderer.MeshUploadCount);
        Assert.Equal(2, renderer.MeshDrawCount);

        camera.Position = new(12, 0);
        camera.Zoom = 2;
        camera.Rotation = MathF.PI / 2;
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color.Transparent);
        renderer.DrawMesh(mesh, new SolidColorShader(Color.Blue),
            Matrix3x2.CreateScale(2, .5f) * Matrix3x2.CreateTranslation(12, 16));
        renderer.EndFrame();
        pixels = graphics.ReadPixels();
        Assert.Equal(Color.Blue, pixels[64 * 128 + 96]);
        Assert.Equal(Color.Transparent, pixels[64 * 128 + 64]);
        Assert.Equal(1, renderer.MeshUploadCount);
    }

    [Theory]
    [InlineData("solid")]
    [InlineData("gradient")]
    [InlineData("sprite")]
    [InlineData("spriteX")]
    [InlineData("spriteY")]
    [InlineData("spriteXY")]
    [InlineData("tile")]
    [InlineData("origin")]
    public void RetainedMaterialsMatchBatchedRendering(string kind)
    {
        using var texture = CreateTexture();
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        IShader2D material = kind switch
        {
            "solid" => new SolidColorShader(new Color(240, 80, 20, 128)),
            "gradient" => new LinearGradientShader(new Color(240, 80, 20, 128), new Color(20, 80, 240, 180)),
            "tile" => new TextureShader2D(texture, new(16), filterMode: TextureFilter.Point),
            "origin" => new TextureShader2D(texture, new(32), filterMode: TextureFilter.Point, imageOrigin: new(-16, 16)),
            _ => new SpriteShader2D(texture, TextureFilter.Point) { FlipX = kind.Contains('X'), FlipY = kind.Contains('Y') }
        };
        var shape = Rectangle2D.Builder.Create(new(-16, -16), new(16, 16));
        var mesh = Quad();
        Color[] Render(bool retained)
        {
            renderer.BeginFrame(128, 128, default);
            renderer.Clear(Color.White);
            if (retained)
            {
                renderer.DrawMesh(mesh, material);
            }
            else
            {
                renderer.Draw(new WorldObject2D(shape, material));
            }
            renderer.EndFrame();
            return graphics.ReadPixels();
        }
        var expected = Render(false);
        var actual = Render(true);
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.InRange(Math.Abs(expected[i].R - actual[i].R), 0, 1);
            Assert.InRange(Math.Abs(expected[i].G - actual[i].G), 0, 1);
            Assert.InRange(Math.Abs(expected[i].B - actual[i].B), 0, 1);
            Assert.InRange(Math.Abs(expected[i].A - actual[i].A), 0, 1);
        }
        Assert.Equal(1, renderer.MeshUploadCount);
    }

    [Fact]
    public void FlipsAndReplacementTexturesDoNotUseStaleMaterialData()
    {
        using var texture = CreateTexture();
        using var replacementBitmap = new Bitmap(1, 1);
        replacementBitmap.SetPixel(0, 0, System.Drawing.Color.Magenta);
        using var replacement = Texture2D.FromBitmap("replacement", replacementBitmap);
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        var mesh = Quad();
        var material = new SpriteShader2D(texture, TextureFilter.Point);
        Color Sample()
        {
            renderer.BeginFrame(128, 128, default);
            renderer.Clear(Color.Transparent);
            renderer.DrawMesh(mesh, material);
            renderer.EndFrame();
            return graphics.ReadPixels()[56 * 128 + 56];
        }
        Assert.Equal(new Color(128, 0, 0, 128), Sample());
        material.FlipX = true;
        Assert.Equal(Color.Lime, Sample());
        Assert.Equal(2, renderer.MeshUploadCount);
        material.FlipX = false;
        material.Texture = replacement;
        Assert.Equal(Color.Magenta, Sample());
        Assert.Equal(2, renderer.MeshUploadCount);
        replacement.Dispose();
        renderer.BeginFrame(128, 128, default);
        Assert.Throws<ObjectDisposedException>(() => renderer.DrawMesh(mesh, material));
        renderer.EndFrame();
    }

    [Fact]
    public void RetainedAndBatchedDrawsKeepPainterOrderAndResetEffectState()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        var mesh = Quad(24);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color.Transparent);
        renderer.DrawShape(Rectangle2D.Builder.CreateFromSize(96, 96), Color.Red);
        renderer.DrawMesh(mesh, new SolidColorShader(new Color(0, 0, 255, 128)), Matrix3x2.CreateTranslation(8, 0));
        renderer.DrawMesh(Quad(8), new LinearGradientShader(Color.Lime, Color.Lime));
        renderer.DrawShape(Rectangle2D.Builder.CreateFromSize(8, 8), Color.Yellow);
        renderer.EndFrame();
        var pixels = graphics.ReadPixels();
        Assert.Equal(Color.Yellow, pixels[64 * 128 + 64]);
        Assert.Equal(Color.Lime, pixels[64 * 128 + 70]);
        Assert.Equal(new Color(127, 0, 128, 255), pixels[64 * 128 + 80]);
        Assert.Equal(Color.Red, pixels[64 * 128 + 30]);
    }

    [Fact]
    public void ChangingCustomMaterialsAreEvaluatedEveryDraw()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        var mesh = Quad(8);
        var material = new ChangingMaterial { BaseColor = Color.Red };
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color.Transparent);
        renderer.DrawMesh(mesh, material, Matrix3x2.CreateTranslation(-24, 0));
        material.BaseColor = Color.Blue;
        renderer.DrawMesh(mesh, material, Matrix3x2.CreateTranslation(24, 0));
        renderer.EndFrame();
        var pixels = graphics.ReadPixels();
        Assert.Equal(Color.Red, pixels[64 * 128 + 40]);
        Assert.Equal(Color.Blue, pixels[64 * 128 + 88]);
        Assert.Equal(0, renderer.MeshUploadCount);
    }

    [Fact]
    public void CacheEvictsLeastRecentlyDrawnMeshesAndCanBeCleared()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        var a = Quad();
        var b = Quad();
        var c = Quad();
        var material = new SolidColorShader(Color.White);
        renderer.BeginFrame(128, 128, default);
        renderer.DrawMesh(a, material);
        var bytes = renderer.CachedMeshBytes;
        Assert.True(bytes > 0);
        renderer.MeshCacheBudgetBytes = bytes * 2;
        renderer.DrawMesh(b, material);
        renderer.DrawMesh(a, material);
        renderer.DrawMesh(c, material);
        renderer.DrawMesh(a, material);
        Assert.Equal(3, renderer.MeshUploadCount);
        Assert.Equal(bytes * 2, renderer.CachedMeshBytes);
        renderer.DrawMesh(b, material);
        Assert.Equal(4, renderer.MeshUploadCount);
        renderer.ClearMeshCache();
        Assert.Equal(0, renderer.CachedMeshBytes);
        renderer.MeshCacheBudgetBytes = 0;
        renderer.DrawMesh(a, material);
        Assert.Equal(0, renderer.CachedMeshBytes);
        renderer.EndFrame();
        renderer.Dispose();
        Assert.Throws<ObjectDisposedException>(() => renderer.ClearMeshCache());
    }

    [Fact]
    public void LargePolygonShapesAutomaticallyUseRetainedBuffers()
    {
        var points = new Vector2[96];
        VertexGenerator2D.WriteCircle(points, Vector2.Zero, 24);
        var shape = SimplePolygon2D.Builder.Create(points);
        var item = new WorldObject2D(shape, new SolidColorShader(Color.Lime));
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color.Transparent);
        renderer.Draw(item);
        renderer.Draw(item);
        renderer.EndFrame();
        Assert.Equal(1, renderer.MeshUploadCount);
        Assert.Equal(2, renderer.MeshDrawCount);
        Assert.Equal(Color.Lime, graphics.ReadPixels()[64 * 128 + 64]);
    }

    [Theory]
    [InlineData(65535, 2)]
    [InlineData(65536, 4)]
    [InlineData(65537, 4)]
    public void IndexBufferWidthPreservesIndicesAtTheSixteenBitBoundary(int vertexCount, int indexBytes)
    {
        var vertices = new Vector2[vertexCount];
        vertices[0] = new(-16, -16);
        vertices[1] = new(16, -16);
        vertices[^1] = new(0, 16);
        var mesh = new TriangleMesh2D(vertices, [0, 1, vertexCount - 1]);
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color.Transparent);
        renderer.DrawMesh(mesh, new SolidColorShader(Color.Lime));
        renderer.EndFrame();
        Assert.Equal(Color.Lime, graphics.ReadPixels()[64 * 128 + 64]);
        Assert.Equal((long)vertexCount * VertexPositionColorTexture.VertexDeclaration.VertexStride + 3 * indexBytes,
            renderer.CachedMeshBytes);
    }

    [Fact]
    public void LargeIndexStreamsSubmitTheFinalPartialChunk()
    {
        var indices = new int[8193 * 3];
        for (var triangle = 0; triangle < 8192; triangle++)
        {
            indices[triangle * 3 + 1] = 1;
            indices[triangle * 3 + 2] = 2;
        }
        indices[^3] = 3;
        indices[^2] = 4;
        indices[^1] = 5;
        var mesh = new TriangleMesh2D(
            [new(-40, -12), new(-16, -12), new(-28, 12), new(16, -12), new(40, -12), new(28, 12)], indices);
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(Color.Transparent);
        renderer.DrawMesh(mesh, new SolidColorShader(Color.Lime));
        renderer.EndFrame();
        var pixels = graphics.ReadPixels();
        Assert.Equal(Color.Lime, pixels[64 * 128 + 36]);
        Assert.Equal(Color.Lime, pixels[64 * 128 + 92]);
        Assert.Equal(2, renderer.MeshDrawCount);
    }

    private sealed class ChangingMaterial : IShader2D
    {
        public Color BaseColor { get; set; }
    }

    private static Texture2D CreateTexture()
    {
        using var bitmap = new Bitmap(2, 2);
        bitmap.SetPixel(0, 0, System.Drawing.Color.FromArgb(128, 255, 0, 0));
        bitmap.SetPixel(1, 0, System.Drawing.Color.Lime);
        bitmap.SetPixel(0, 1, System.Drawing.Color.Blue);
        bitmap.SetPixel(1, 1, System.Drawing.Color.Yellow);
        return Texture2D.FromBitmap("mesh-test", bitmap);
    }
}
