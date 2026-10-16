using App2d.Core.Rendering;
using System.Numerics;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Tests.Rendering;

public sealed class ColorRgba32Tests
{
    [Fact]
    public void HexParsingKeepsTheChannelOrderAndFormatsCanonicalLowercase()
    {
        Assert.Equal(new ColorRgba32(0x12, 0xab, 0xef), ColorRgba32.ParseRgb("#12abEF"));
        var color = ColorRgba32.ParseRgba("12abEF80");
        Assert.Equal(new ColorRgba32(0x12, 0xab, 0xef, 0x80), color);
        Assert.Equal("#12abef", color.ToHexRgb());
        Assert.Equal("12abef80", color.ToHexRgba());
        Assert.Equal(new Color(0x12, 0xab, 0xef, 0x80), color.ToColor());
        Assert.Equal(new Color(0x12, 0xab, 0xef, 0x80), ColorExtensions.FromHexRgba("12abEF80"));
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
        Assert.False(ColorRgba32.TryParseRgb(text, out _));
        Assert.Throws<FormatException>(() => ColorRgba32.ParseRgb(text));
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
        Assert.False(ColorRgba32.TryParseRgba(text, out _));
        Assert.Throws<FormatException>(() => ColorRgba32.ParseRgba(text));
    }

    [Fact]
    public void NormalizedConversionClampsRoundsAndRoundTripsEveryByte()
    {
        Assert.Equal(new ColorRgba32(0, 128, 255, 255), ColorRgba32.FromVector4(new(-1, .5f, 1, 2)));
        for (var i = 0; i < 256; i++)
        {
            var color = new ColorRgba32((byte)i, (byte)(255 - i), 47, 123);
            Assert.Equal(color, ColorRgba32.FromVector4(color.ToVector4()));
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => ColorRgba32.FromVector4(new(float.NaN)));
        Assert.Throws<ArgumentNullException>(() => ColorRgba32.ParseRgb(null!));
        Assert.False(ColorRgba32.TryParseRgb(null, out _));
        Assert.False(ColorRgba32.TryParseRgba(null, out _));
    }
}
