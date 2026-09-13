using App2d.Core.Geometry;
using App2d.Rendering;
using App2d.Rendering.Textures;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using XnaColor = Microsoft.Xna.Framework.Color;
using Texture2D = App2d.Rendering.Textures.Texture2D;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class MonoGameRenderingTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void SpritePreservesImageOrientationAndFlips(bool flipX, bool flipY)
    {
        using var texture = CreateTexture();
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        var sprite = new SpriteShader2D(texture, TextureFilter.Point) { FlipX = flipX, FlipY = flipY };
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(XnaColor.Transparent);
        renderer.Draw(new WorldObject2D(Rectangle2D.FromSize(new Vector2(64)), sprite));
        renderer.EndFrame();
        var pixels = graphics.ReadPixels();
        XnaColor[] expected = [XnaColor.Red, XnaColor.Lime, XnaColor.Blue, XnaColor.Yellow];
        for (var y = 0; y < 2; y++)
            for (var x = 0; x < 2; x++)
                Assert.Equal(expected[(flipY ? 1 - y : y) * 2 + (flipX ? 1 - x : x)], pixels[(48 + 32 * y) * 128 + 48 + 32 * x]);
    }

    [Fact]
    public void TextureUploadPreservesChannelsAndBlendsStraightAlphaOnce()
    {
        using var texture = CreateTexture(translucent: true);
        Assert.Equal(new XnaColor(255, 0, 0, 128), texture.CopyPixels()[0]);
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(XnaColor.Blue);
        renderer.DrawScreenTexture(texture, new(0, 0, 128, 128));
        renderer.EndFrame();
        var color = graphics.ReadPixels()[16 * 128 + 16];
        Assert.InRange(color.R, (byte)127, (byte)129);
        Assert.Equal(0, color.G);
        Assert.InRange(color.B, (byte)126, (byte)128);
        Assert.Equal(255, color.A);
    }

    [Fact]
    public void TiledTextureRepeatsAtLocalOriginAndKeepsItsOriginalYConvention()
    {
        using var texture = CreateTexture();
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(XnaColor.Transparent);
        renderer.Draw(new WorldObject2D(Rectangle2D.FromSize(new Vector2(64)),
            new TextureShader2D(texture, new Vector2(32), filterMode: TextureFilter.Point)));
        renderer.EndFrame();
        var pixels = graphics.ReadPixels();
        Assert.Equal(XnaColor.Blue, pixels[40 * 128 + 40]);
        Assert.Equal(pixels[40 * 128 + 40], pixels[40 * 128 + 72]);
        Assert.Equal(pixels[40 * 128 + 40], pixels[72 * 128 + 40]);
    }

    [Fact]
    public void GradientAndLayerOrderSurviveMaterialBatchChanges()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        var scene = new Scene2D();
        scene.Add(new WorldObject2D(Rectangle2D.FromSize(new Vector2(64)),
            new SolidColorShader(XnaColor.Lime)) { ZIndex = 1 });
        scene.Add(new WorldObject2D(Rectangle2D.FromSize(new Vector2(96)),
            new LinearGradientShader(XnaColor.Red, XnaColor.Blue)));
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(XnaColor.Transparent);
        renderer.Draw(scene);
        renderer.EndFrame();
        var pixels = graphics.ReadPixels();
        Assert.Equal(XnaColor.Lime, pixels[64 * 128 + 64]);
        Assert.True(pixels[20 * 128 + 64].R > pixels[20 * 128 + 64].B);
        Assert.True(pixels[108 * 128 + 64].B > pixels[108 * 128 + 64].R);
    }

    [Fact]
    public void ResizeAndTextureUnloadLeaveRendererUsable()
    {
        using var texture = CreateTexture();
        using var graphics = new GraphicsTestContext();
        var camera = new Camera2D();
        using var renderer = new Renderer2D(camera, graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.DrawScreenTexture(texture, new(0, 0, 64, 64));
        renderer.EndFrame();
        texture.Dispose();
        renderer.BeginFrame(64, 96, default);
        renderer.Clear(XnaColor.Blue);
        renderer.DrawScreenRoundedRectangle(new(4, 4, 40, 40), 8, XnaColor.Red);
        renderer.DrawScreenText("HUD — 120", new(4, 80), XnaColor.White);
        renderer.EndFrame();
        Assert.Equal(new Vector2(64, 96), camera.ViewportSize);
        Assert.Equal(XnaColor.Red, graphics.ReadPixels()[20 * 128 + 20]);
        Assert.Throws<ObjectDisposedException>(() => texture.CopyPixels());
    }

    private static Texture2D CreateTexture(bool translucent = false)
    {
        var path = Path.Combine(Path.GetTempPath(), $"app2d-render-{Guid.NewGuid():N}.png");
        try
        {
            using var bitmap = new System.Drawing.Bitmap(2, 2);
            bitmap.SetPixel(0, 0, System.Drawing.Color.FromArgb(translucent ? 128 : 255, 255, 0, 0));
            bitmap.SetPixel(1, 0, System.Drawing.Color.Lime);
            bitmap.SetPixel(0, 1, System.Drawing.Color.Blue);
            bitmap.SetPixel(1, 1, System.Drawing.Color.Yellow);
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return Texture2D.Load(path);
        }
        finally { File.Delete(path); }
    }
}
