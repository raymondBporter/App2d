using App2d.Rendering;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Tests.Rendering;

public sealed class ColorExtensionsTests
{
    [Fact]
    public void WithAlphaReplacesExistingAlphaAndClampsFraction()
    {
        var color = new Color(20, 40, 60, 90);
        Assert.Equal(new Color(20, 40, 60, 128), color.WithAlpha(.5f));
        Assert.Equal(new Color(20, 40, 60, 0), color.WithAlpha(-1f));
        Assert.Equal(new Color(20, 40, 60, 255), color.WithAlpha(2f));
        Assert.Equal((byte)90, color.A);
    }

    [Fact]
    public void ScaleAlphaMultipliesExistingAlphaWithoutChangingRgb()
    {
        var color = new Color(20, 40, 60, 255);
        Assert.Equal(new Color(20, 40, 60, 127), color.ScaleAlpha(.5f));
        Assert.Equal(new Color(20, 40, 60, 255), color.ScaleAlpha(2f));
        Assert.Equal(new Color(20, 40, 60, 0), color.ScaleAlpha(-1f));
    }

    [Fact]
    public void ScaleRgbPreservesAlphaAndClampsChannels()
    {
        var color = new Color(100, 200, 50, 64);
        Assert.Equal(new Color(72, 144, 36, 64), color.ScaleRgb(.72f));
        Assert.Equal(new Color(255, 255, 150, 64), color.ScaleRgb(3f));
        Assert.Equal(new Color(0, 0, 0, 64), color.ScaleRgb(-1f));
    }

    [Fact]
    public void HexRgbParsingIsReusableOutsideCharacterJson()
    {
        Assert.Equal(new Color(0x12, 0xAB, 0xEF), ColorExtensions.FromHexRgb("#12abEF"));
        Assert.Throws<FormatException>(() => ColorExtensions.FromHexRgb("12abEF"));
        Assert.Throws<FormatException>(() => ColorExtensions.FromHexRgb("#xx0000"));
    }

    [Fact]
    public void NonFiniteFactorsAreRejected()
    {
        var color = Color.White;
        Assert.Throws<ArgumentOutOfRangeException>(() => color.WithAlpha(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => color.ScaleAlpha(float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => color.ScaleRgb(float.NegativeInfinity));
    }
}
