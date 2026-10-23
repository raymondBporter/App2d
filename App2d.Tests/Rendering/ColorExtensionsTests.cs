using App2d.Core.Rendering;
using System.Numerics;

namespace App2d.Tests.Rendering;

public sealed class ColorExtensionsTests
{
    [Fact]
    public void WithAlphaReplacesExistingAlphaAndClampsFraction()
    {
        var color = new Color4b(20, 40, 60, 90);
        Assert.Equal(new Color4b(20, 40, 60, 128), color.WithAlpha(.5f));
        Assert.Equal(new Color4b(20, 40, 60, 0), color.WithAlpha(-1f));
        Assert.Equal(new Color4b(20, 40, 60, 255), color.WithAlpha(2f));
        Assert.Equal((byte)90, color.A);
    }

    [Fact]
    public void ScaleAlphaMultipliesExistingAlphaWithoutChangingRgb()
    {
        var color = new Color4b(20, 40, 60, 255);
        Assert.Equal(new Color4b(20, 40, 60, 127), color.ScaleAlpha(.5f));
        Assert.Equal(new Color4b(20, 40, 60, 255), color.ScaleAlpha(2f));
        Assert.Equal(new Color4b(20, 40, 60, 0), color.ScaleAlpha(-1f));
    }

    [Fact]
    public void ScaleRgbPreservesAlphaAndClampsChannels()
    {
        var color = new Color4b(100, 200, 50, 64);
        Assert.Equal(new Color4b(72, 144, 36, 64), color.ScaleRgb(.72f));
        Assert.Equal(new Color4b(255, 255, 150, 64), color.ScaleRgb(3f));
        Assert.Equal(new Color4b(0, 0, 0, 64), color.ScaleRgb(-1f));
    }

    [Fact]
    public void HexParsingKeepsTheChannelOrderAndFormatsCanonicalLowercase()
    {
        Assert.Equal(new Color4b(0x12, 0xab, 0xef), ColorExtensions.FromHexRgb("#12abEF"));
        var color = ColorExtensions.FromHexRgba("12abEF80");
        Assert.Equal(new Color4b(0x12, 0xab, 0xef, 0x80), color);
        Assert.Equal("#12abef", color.ToHexRgb());
        Assert.Equal("12abef80", color.ToHexRgba());
        Assert.True(ColorExtensions.TryParseRgb("#12abEF", out var rgb));
        Assert.Equal(new Color4b(0x12, 0xab, 0xef), rgb);
        Assert.True(ColorExtensions.TryParseRgba("12abEF80", out var rgba));
        Assert.Equal(color, rgba);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#abc")]
    [InlineData("12abef")]
    [InlineData("#12xzef")]
    [InlineData("# 12345")]
    [InlineData("#123456 ")]
    public void RgbRequiresTheCompleteHashPrefixedFormat(string text)
    {
        Assert.False(ColorExtensions.TryParseRgb(text, out var color));
        Assert.Equal(default, color);
        Assert.Throws<FormatException>(() => ColorExtensions.FromHexRgb(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12345678")]
    [InlineData("1234567")]
    [InlineData("1234567x")]
    [InlineData(" 1234567")]
    [InlineData("1234567 ")]
    public void RgbaRequiresExactlyEightHexDigits(string text)
    {
        Assert.False(ColorExtensions.TryParseRgba(text, out var color));
        Assert.Equal(default, color);
        Assert.Throws<FormatException>(() => ColorExtensions.FromHexRgba(text));
    }

    [Fact]
    public void NormalizedConversionClampsRoundsAndRoundTripsEveryByte()
    {
        Assert.Equal(new Color4b(0, 128, 255, 255), ColorExtensions.FromVector4(new(-1, .5f, 1, 2)));
        Assert.Equal(new Vector4(0, 128, 255, 64) / 255, new Color4b(0, 128, 255, 64).ToNumericsVector4());
        for (var i = 0; i < 256; i++)
        {
            var color = new Color4b((byte)i, (byte)(255 - i), (byte)47, (byte)i);
            Assert.Equal(color, ColorExtensions.FromVector4(color.ToNumericsVector4()));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorExtensions.FromVector4(new(float.NaN)));
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorExtensions.FromVector4(new(float.PositiveInfinity)));
        Assert.Throws<ArgumentNullException>(() => ColorExtensions.FromHexRgb(null!));
        Assert.Throws<ArgumentNullException>(() => ColorExtensions.FromHexRgba(null!));
        Assert.False(ColorExtensions.TryParseRgb(null, out _));
        Assert.False(ColorExtensions.TryParseRgba(null, out _));
    }

    [Fact]
    public void NonFiniteFactorsAreRejected()
    {
        var color = Color4b.White;
        Assert.Throws<ArgumentOutOfRangeException>(() => color.WithAlpha(float.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => color.ScaleAlpha(float.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => color.ScaleRgb(float.NegativeInfinity));
    }
}
