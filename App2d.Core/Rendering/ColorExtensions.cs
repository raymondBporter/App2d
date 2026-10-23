using App2d.Core.Validation;
using System.Globalization;
using System.Numerics;

namespace App2d.Core.Rendering;

/// <summary>Color operations shared by renderers. Alpha and RGB channels are stored as straight, unpremultiplied values.</summary>
public static class ColorExtensions
{
    /// <summary>Replaces alpha with a normalized value, preserving RGB.</summary>
    public static Color4b WithAlpha(this Color4b color, float alpha)
    {
        ArgGuard.Finite(alpha);
        color.A = (byte)MathF.Round(Math.Clamp(alpha, 0f, 1f) * 255f);
        return color;
    }

    /// <summary>Scales the existing alpha by a normalized opacity, preserving RGB.</summary>
    public static Color4b ScaleAlpha(this Color4b color, float opacity)
    {
        ArgGuard.Finite(opacity);
        color.A = (byte)(color.A * Math.Clamp(opacity, 0f, 1f));
        return color;
    }

    /// <summary>Scales RGB channels, preserving alpha. Results are clamped to byte range and truncated.</summary>
    public static Color4b ScaleRgb(this Color4b color, float factor)
    {
        ArgGuard.Finite(factor);
        color.R = (byte)Math.Clamp(color.R * factor, 0f, 255f);
        color.G = (byte)Math.Clamp(color.G * factor, 0f, 255f);
        color.B = (byte)Math.Clamp(color.B * factor, 0f, 255f);
        return color;
    }

    /// <summary>Parses an opaque #RRGGBB color.</summary>
    public static Color4b FromHexRgb(string hex)
    {
        ArgGuard.ThrowIfNull(hex);
        return TryParseRgb(hex, out var color) ? color : throw new FormatException("Expected a #RRGGBB color.");
    }

    /// <summary>Parses eight RGBA hexadecimal digits.</summary>
    public static Color4b FromHexRgba(string hex)
    {
        ArgGuard.ThrowIfNull(hex);
        return TryParseRgba(hex, out var color) ? color : throw new FormatException("Expected eight RGBA hexadecimal digits.");
    }

    public static bool TryParseRgb(ReadOnlySpan<char> text, out Color4b color)
    {
        if (text.Length == 7 && text[0] == '#' && uint.TryParse(text[1..], NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgb))
        {
            color = new((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
            return true;
        }
        color = default;
        return false;
    }

    public static bool TryParseRgba(ReadOnlySpan<char> text, out Color4b color)
    {
        if (text.Length == 8 && uint.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var rgba))
        {
            color = new((byte)(rgba >> 24), (byte)(rgba >> 16), (byte)(rgba >> 8), (byte)rgba);
            return true;
        }
        color = default;
        return false;
    }

    public static string ToHexRgb(this Color4b color) => $"#{color.R:x2}{color.G:x2}{color.B:x2}";
    public static string ToHexRgba(this Color4b color) => $"{color.R:x2}{color.G:x2}{color.B:x2}{color.A:x2}";

    /// <summary>Converts byte channels to normalized System.Numerics components without premultiplying alpha.</summary>
    public static Vector4 ToNumericsVector4(this Color4b color) => new Vector4(color.R, color.G, color.B, color.A) / 255;

    /// <summary>Clamps normalized System.Numerics components and rounds them to the nearest byte without premultiplying alpha.</summary>
    public static Color4b FromVector4(Vector4 color)
    {
        ArgGuard.Finite(color);
        color = Vector4.Clamp(color, Vector4.Zero, Vector4.One);
        return new((byte)MathF.Round(color.X * 255), (byte)MathF.Round(color.Y * 255),
            (byte)MathF.Round(color.Z * 255), (byte)MathF.Round(color.W * 255));
    }
}
