using App2d.Core.Validation;
using System.Globalization;
using Color = Microsoft.Xna.Framework.Color;

namespace App2d.Rendering;

/// <summary>Color operations shared by renderers. Alpha and RGB channels are stored as straight, unpremultiplied values.</summary>
public static class ColorExtensions
{
    /// <summary>Replaces alpha with a normalized value, preserving RGB.</summary>
    public static Color WithAlpha(this Color color, float alpha)
    {
        ArgGuard.ThrowIfNotFinite(alpha);
        color.A = (byte)MathF.Round(Math.Clamp(alpha, 0f, 1f) * 255f);
        return color;
    }

    /// <summary>Scales the existing alpha by a normalized opacity, preserving RGB.</summary>
    public static Color ScaleAlpha(this Color color, float opacity)
    {
        ArgGuard.ThrowIfNotFinite(opacity);
        color.A = (byte)(color.A * Math.Clamp(opacity, 0f, 1f));
        return color;
    }

    /// <summary>Scales RGB channels, preserving alpha. Results are clamped to byte range and truncated.</summary>
    public static Color ScaleRgb(this Color color, float factor)
    {
        ArgGuard.ThrowIfNotFinite(factor);
        color.R = (byte)Math.Clamp(color.R * factor, 0f, 255f);
        color.G = (byte)Math.Clamp(color.G * factor, 0f, 255f);
        color.B = (byte)Math.Clamp(color.B * factor, 0f, 255f);
        return color;
    }

    /// <summary>Parses an opaque #RRGGBB color.</summary>
    public static Color FromHexRgb(string hex)
    {
        ArgGuard.ThrowIfNull(hex);
        if (hex.Length != 7 || hex[0] != '#') throw new FormatException("Expected a #RRGGBB color.");
        return new(
            byte.Parse(hex.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(hex.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }
}
