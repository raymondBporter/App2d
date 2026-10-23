using App2d.Core.Rendering;
using App2d.Rendering;
using System.Numerics;

namespace App2d.Tests.Rendering;

[Collection("Graphics")]
public sealed class ScaledScreenTextTests
{
    [Fact]
    public void WrappedTextUsesTheSameMeasuredFontAndRenderingAsExplicitLines()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        const float scale = .5f;
        var width = renderer.MeasureScreenText("WIDE WORD", scale);
        var wrapped = renderer.WrapScreenText("WIDE WORD WIDE WORD", width, scale, maxLines: 2);
        Assert.Contains('\n', wrapped);
        Assert.All(wrapped.Split('\n'), line => Assert.True(renderer.MeasureScreenText(line, scale) <= width + .001f));
        var automatic = Draw(true);
        var explicitLines = Draw(false);
        Assert.Equal(explicitLines, automatic);
        Assert.Equal("", renderer.WrapScreenText("", width));
        Assert.ThrowsAny<ArgumentException>(() => renderer.WrapScreenText("text", 0));
        Assert.ThrowsAny<ArgumentException>(() => renderer.WrapScreenText("text", width, maxLines: 0));

        Color4b[] Draw(bool automaticWrap)
        {
            renderer.BeginFrame(128, 128, default);
            renderer.Clear(Color4b.Black);
            if (automaticWrap) renderer.DrawScreenTextWrapped("WIDE WORD WIDE WORD", new(10, 40), width, Color4b.White, scale, maxLines: 2);
            else renderer.DrawScreenText(wrapped, new(10, 40), Color4b.White, scale);
            renderer.EndFrame();
            return graphics.ReadPixels();
        }
    }

    [Fact]
    public void HalfSizeTextUsesTheSameAtlasAndLeavesSubsequentScreenCoordinatesUnscaled()
    {
        using var graphics = new GraphicsTestContext();
        using var renderer = new Renderer2D(new Camera2D(), graphics.Device);
        var full = Draw(1);
        var small = Draw(.5f);
        var fullWidth = InkWidth(full);
        var smallWidth = InkWidth(small);
        Assert.InRange(smallWidth, fullWidth * .45f, fullWidth * .6f);
        Assert.Equal(Color4b.Red, small[110 * 128 + 110]);
        Assert.Equal(Color4b.Red, full[110 * 128 + 110]);

        Color4b[] Draw(float scale)
        {
            renderer.BeginFrame(128, 128, default);
            renderer.Clear(Color4b.Black);
            renderer.DrawScreenText("TEST", new Vector2(10, 55), Color4b.White, scale);
            renderer.DrawScreenRoundedRectangle(new(105, 105, 120, 120), 0, Color4b.Red);
            renderer.EndFrame();
            return graphics.ReadPixels();
        }

        static int InkWidth(Color4b[] pixels) => Enumerable.Range(0, 128 * 90)
            .Where(i => pixels[i].R > 25).Max(i => i % 128) - 10;
    }
}
