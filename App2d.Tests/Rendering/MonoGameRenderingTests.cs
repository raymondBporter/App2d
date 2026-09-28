using App2d.Core.Geometry;
using App2d.Core.Shapes;
using App2d.Rendering;
using App2d.Rendering.Textures;
using Microsoft.Xna.Framework.Graphics;
using System.Numerics;
using Texture2D = App2d.Rendering.Textures.Texture2D;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class MonoGameRenderingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TriangleRendersFilledAndWithAnOutline(bool overlay)
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        var triangle = new Triangle2D(new(-16, -12), new(16, -12), new(0, 16));
        var item = new WorldObject2D(triangle, new SolidColorShader(XnaColor.Lime));
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(XnaColor.Transparent);
        if (overlay) renderer.DrawShapeOverlay(item, XnaColor.Lime, XnaColor.White);
        else renderer.Draw(item);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(XnaColor.Lime, pixels[64 * 128 + 64]);
        if (overlay) Assert.Equal(XnaColor.White, pixels[76 * 128 + 64]);
        Assert.Equal(XnaColor.Transparent, pixels[84 * 128 + 64]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HalfSpacesStillRenderWithObjectOwnedBoundsAndRejectSprites(bool overlay)
    {
        using var texture = CreateTexture();
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        var halfSpace = new HalfSpace2D(Vector2.UnitY, 0);
        var item = new WorldObject2D(halfSpace, new SolidColorShader(XnaColor.Blue));
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(XnaColor.Transparent);
        if (overlay) renderer.DrawShapeOverlay(item, XnaColor.Blue, XnaColor.White);
        else renderer.Draw(item);
        Assert.Throws<InvalidOperationException>(() =>
            renderer.Draw(new WorldObject2D(halfSpace, new SpriteShader2D(texture, TextureFilter.Point))));
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(Bounds2D.Unbounded, item.LocalBounds);
        Assert.Equal(XnaColor.Blue, pixels[80 * 128 + 64]);
        Assert.Equal(XnaColor.Transparent, pixels[48 * 128 + 64]);
    }

    [Fact]
    public void SharedContoursRenderRoundPrimitivesInWorldAndScreenCoordinates()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(XnaColor.Transparent);
        renderer.Draw(new WorldObject2D(new Circle2D(12, new(-30, 20)), new SolidColorShader(XnaColor.Lime)));
        renderer.Draw(new WorldObject2D(new Capsule2D(new(10, 20), new(35, 20), 8), new SolidColorShader(XnaColor.Blue)));
        renderer.DrawScreenRoundedRectangle(new(10, 95, 60, 120), 10, XnaColor.Red);
        renderer.DrawWorldCircle(new(0, -10), 8, XnaColor.White);
        renderer.EndFrame();

        var pixels = graphics.ReadPixels();
        Assert.Equal(XnaColor.Lime, pixels[44 * 128 + 34]);
        Assert.Equal(XnaColor.Transparent, pixels[44 * 128 + 48]);
        Assert.Equal(XnaColor.Blue, pixels[44 * 128 + 80]);
        Assert.Equal(XnaColor.Blue, pixels[44 * 128 + 103]);
        Assert.Equal(XnaColor.Transparent, pixels[55 * 128 + 80]);
        Assert.Equal(XnaColor.Red, pixels[107 * 128 + 35]);
        Assert.Equal(XnaColor.Transparent, pixels[95 * 128 + 10]);
        Assert.Equal(XnaColor.White, pixels[74 * 128 + 72]);
        Assert.Equal(XnaColor.Transparent, pixels[74 * 128 + 64]);
    }

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
        {
            for (var x = 0; x < 2; x++)
                Assert.Equal(expected[(flipY ? 1 - y : y) * 2 + (flipX ? 1 - x : x)], pixels[(48 + 32 * y) * 128 + 48 + 32 * x]);
        }
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
    public void TiledTextureWithImageOriginDrawsUprightFromThatCorner()
    {
        using var texture = CreateTexture();
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        renderer.BeginFrame(128, 128, default);
        renderer.Clear(XnaColor.Transparent);
        renderer.Draw(new WorldObject2D(Rectangle2D.FromSize(new Vector2(64)),
            new TextureShader2D(texture, new Vector2(64), filterMode: TextureFilter.Point, imageOrigin: new Vector2(-32, 32))));
        renderer.EndFrame();
        var pixels = graphics.ReadPixels();
        Assert.Equal(XnaColor.Red, pixels[40 * 128 + 40]);
        Assert.Equal(XnaColor.Lime, pixels[40 * 128 + 88]);
        Assert.Equal(XnaColor.Blue, pixels[88 * 128 + 40]);
    }

    [Fact]
    public void GradientAndLayerOrderSurviveMaterialBatchChanges()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        var scene = new Scene2D();
        scene.Add(new WorldObject2D(Rectangle2D.FromSize(new Vector2(64)),
            new SolidColorShader(XnaColor.Lime))
        { ZIndex = 1 });
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
            using var bitmap = new Bitmap(2, 2);
            bitmap.SetPixel(0, 0, Color.FromArgb(translucent ? 128 : 255, 255, 0, 0));
            bitmap.SetPixel(1, 0, Color.Lime);
            bitmap.SetPixel(0, 1, Color.Blue);
            bitmap.SetPixel(1, 1, Color.Yellow);
            bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            return Texture2D.Load(path);
        }
        finally { File.Delete(path); }
    }
}
