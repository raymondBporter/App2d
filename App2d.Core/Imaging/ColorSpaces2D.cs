using App2d.Core.Validation;
using System.Numerics;

namespace App2d.Core.Imaging;

/// <summary>Explicit conversions from normalized sRGB to HSV or perceptual Oklab channels; alpha is separate.</summary>
public static class ColorSpaces2D
{
    /// <summary>
    /// Converts normalized sRGB to HSV. Hue is a turn in [0, 1), saturation and value
    /// are in [0, 1]. Achromatic colors use hue zero. Hue is circular and unreliable
    /// at low saturation; scalar derivatives across its 0/1 seam are not color distances.
    /// </summary>
    public static Vector3 SrgbToHsv(Vector3 srgb)
    {
        ArgGuard.InRange(srgb, Vector3.Zero, Vector3.One);
        var value = MathF.Max(srgb.X, MathF.Max(srgb.Y, srgb.Z));
        var chroma = value - MathF.Min(srgb.X, MathF.Min(srgb.Y, srgb.Z));
        if (chroma == 0)
        {
            return new(0, 0, value);
        }
        var sector = value == srgb.X ? (srgb.Y - srgb.Z) / chroma
            : value == srgb.Y ? 2 + (srgb.Z - srgb.X) / chroma
            : 4 + (srgb.X - srgb.Y) / chroma;
        var hue = sector / 6;
        return new(hue < 0 ? hue + 1 : hue, chroma / value, value);
    }

    /// <summary>
    /// Converts sRGB components in [0, 1] to Oklab (L, a, b), including sRGB transfer decoding.
    /// Coefficients: https://bottosson.github.io/posts/oklab/
    /// </summary>
    public static Vector3 SrgbToOklab(Vector3 srgb)
    {
        ArgGuard.InRange(srgb, Vector3.Zero, Vector3.One);
        var r = Decode(srgb.X);
        var g = Decode(srgb.Y);
        var b = Decode(srgb.Z);
        var l = MathF.Cbrt(.4122214708f * r + .5363325363f * g + .0514459929f * b);
        var m = MathF.Cbrt(.2119034982f * r + .6806995451f * g + .1073969566f * b);
        var s = MathF.Cbrt(.0883024619f * r + .2817188376f * g + .6299787005f * b);
        return new(
            .2104542553f * l + .7936177850f * m - .0040720468f * s,
            1.9779984951f * l - 2.4285922050f * m + .4505937099f * s,
            .0259040371f * l + .7827717662f * m - .8086757660f * s);
    }

    /// <summary>
    /// Converts Oklab to sRGB, including sRGB transfer encoding. Does not clamp:
    /// colors outside the sRGB gamut can produce components outside [0, 1].
    /// </summary>
    public static Vector3 OklabToSrgb(Vector3 oklab)
    {
        ArgGuard.Finite(oklab);
        var l = oklab.X + .3963377774f * oklab.Y + .2158037573f * oklab.Z;
        var m = oklab.X - .1055613458f * oklab.Y - .0638541728f * oklab.Z;
        var s = oklab.X - .0894841775f * oklab.Y - 1.2914855480f * oklab.Z;
        l = l * l * l;
        m = m * m * m;
        s = s * s * s;
        return new(
            Encode(4.0767416621f * l - 3.3077115913f * m + .2309699292f * s),
            Encode(-1.2684380046f * l + 2.6097574011f * m - .3413193965f * s),
            Encode(-.0041960863f * l - .7034186147f * m + 1.7076147010f * s));
    }

    private static float Decode(float value) =>
        value <= .04045f ? value / 12.92f : MathF.Pow((value + .055f) / 1.055f, 2.4f);

    private static float Encode(float value) =>
        value <= .0031308f ? 12.92f * value : 1.055f * MathF.Pow(value, 1 / 2.4f) - .055f;
}
