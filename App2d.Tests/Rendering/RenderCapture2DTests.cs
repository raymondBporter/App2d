using App2d.Core.Rendering;
using Microsoft.Xna.Framework.Graphics;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class RenderCapture2DTests
{
    [Fact]
    public void CreateTargetUsesTheProofRenderFormat()
    {
        using var graphics = new GraphicsTestContext();
        using var target = RenderCapture2D.CreateTarget(graphics.Device, 48, 32);

        Assert.Equal((48, 32), (target.Width, target.Height));
        Assert.Equal(SurfaceFormat.Color, target.Format);
        Assert.Equal(DepthFormat.Depth24, target.DepthStencilFormat);
        Assert.Equal(RenderTargetUsage.DiscardContents, target.RenderTargetUsage);
    }

    [Fact]
    public void UseRenderTargetBindsClearsAndRestoresThePreviousBinding()
    {
        using var graphics = new GraphicsTestContext();
        using var target = RenderCapture2D.CreateTarget(graphics.Device, 16, 16);
        using (graphics.Device.UseRenderTarget(target, XnaColor.Red))
        {
            Assert.Same(target, Assert.Single(graphics.Device.GetRenderTargets()).RenderTarget);
        }

        Assert.Same(graphics.Target, Assert.Single(graphics.Device.GetRenderTargets()).RenderTarget);
        var pixels = new XnaColor[16 * 16];
        target.GetData(pixels);
        Assert.All(pixels, pixel => Assert.Equal(XnaColor.Red, pixel));
    }

    [Fact]
    public void SavePngRendersANewTargetAndWritesIt()
    {
        using var graphics = new GraphicsTestContext();
        var path = TemporaryPng();
        try
        {
            RenderCapture2D.SavePng(graphics.Device, 8, 8, path, XnaColor.Blue,
                () => Assert.Equal(8, Assert.IsType<RenderTarget2D>(Assert.Single(graphics.Device.GetRenderTargets()).RenderTarget).Width));

            Assert.Same(graphics.Target, Assert.Single(graphics.Device.GetRenderTargets()).RenderTarget);
            Assert.All(Decode(graphics.Device, path, 8, 8), pixel => Assert.Equal(XnaColor.Blue, pixel));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SavePngReusesAnExistingTargetForEachFrame()
    {
        using var graphics = new GraphicsTestContext();
        using var target = RenderCapture2D.CreateTarget(graphics.Device, 8, 8);
        var path = TemporaryPng();
        try
        {
            target.SavePng(path, XnaColor.Lime, () => Assert.Same(target, Assert.Single(graphics.Device.GetRenderTargets()).RenderTarget));

            Assert.Same(graphics.Target, Assert.Single(graphics.Device.GetRenderTargets()).RenderTarget);
            Assert.All(Decode(graphics.Device, path, 8, 8), pixel => Assert.Equal(XnaColor.Lime, pixel));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SavePngUnbindsABoundTargetSoMultisampledContentIsResolved()
    {
        using var graphics = new GraphicsTestContext();
        using var target = RenderCapture2D.CreateTarget(graphics.Device, 8, 8);
        var path = TemporaryPng();
        try
        {
            graphics.Device.SetRenderTarget(target);
            graphics.Device.Clear(XnaColor.Lime);
            target.SavePng(path);

            Assert.Empty(graphics.Device.GetRenderTargets());
            Assert.All(Decode(graphics.Device, path, 8, 8), pixel => Assert.Equal(XnaColor.Lime, pixel));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string TemporaryPng() => Path.Combine(Path.GetTempPath(), $"app2d-capture-{Guid.NewGuid():N}.png");

    private static XnaColor[] Decode(GraphicsDevice device, string path, int width, int height)
    {
        using var stream = File.OpenRead(path);
        using var image = Texture2D.FromStream(device, stream);
        Assert.Equal((width, height), (image.Width, image.Height));
        var pixels = new XnaColor[width * height];
        image.GetData(pixels);
        return pixels;
    }
}
