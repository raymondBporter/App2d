using App2d.Core.Validation;
using System.Globalization;
using System.Numerics;

namespace App2d.Core.Rendering;

/// <summary>Four straight, unpremultiplied byte channels, independent of graphics APIs. Float colors use Vector4.</summary>
public readonly record struct ColorRgba32(byte R, byte G, byte B, byte A = 255)
{
    public Vector4 ToVector4() => new Vector4(R, G, B, A) / 255;

    /// <summary>Clamps normalized channels and rounds them to the nearest byte.</summary>
    public static ColorRgba32 FromVector4(Vector4 color)
    {
        ArgGuard.ThrowIfNotFinite(color);
        color = Vector4.Clamp(color, Vector4.Zero, Vector4.One);
        return new((byte)MathF.Round(color.X * 255), (byte)MathF.Round(color.Y * 255),
            (byte)MathF.Round(color.Z * 255), (byte)MathF.Round(color.W * 255));
    }

    public static bool TryParseRgb(ReadOnlySpan<char> text, out ColorRgba32 color)
    {
        if (text.Length == 7 && text[0] == '#' && uint.TryParse(text[1..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
        {
            color = new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            return true;
        }
        color = default;
        return false;
    }

    public static bool TryParseRgba(ReadOnlySpan<char> text, out ColorRgba32 color)
    {
        if (text.Length == 8 && uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgba))
        {
            color = new((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
            return true;
        }
        color = default;
        return false;
    }

    public static ColorRgba32 ParseRgb(string text)
    {
        ArgGuard.ThrowIfNull(text);
        return TryParseRgb(text, out var color) ? color : throw new FormatException("Expected a #RRGGBB color.");
    }

    public static ColorRgba32 ParseRgba(string text)
    {
        ArgGuard.ThrowIfNull(text);
        return TryParseRgba(text, out var color) ? color : throw new FormatException("Expected eight RGBA hexadecimal digits.");
    }

    public string ToHexRgb() => $"#{R:x2}{G:x2}{B:x2}";
    public string ToHexRgba() => $"{R:x2}{G:x2}{B:x2}{A:x2}";
}
