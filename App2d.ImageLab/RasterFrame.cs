using App2d.Core.Imaging;
using App2d.Core.IO;
using System.Drawing.Imaging;
using System.Numerics;
using System.Runtime.InteropServices;
using CpuTexture = App2d.Core.Rendering.Textures.Texture2D;

namespace App2d.ImageLab;

// A stage retains its actual pixel type. Display/export conversion never becomes the next input.
internal sealed class RasterFrame
{
    public Image2D<Color4b>? Colors { get; }
    public Image2D<float>? Channel { get; }
    public Image2D<bool>? Mask { get; }
    public string Details { get; }
    public int Width { get; }
    public int Height { get; }
    public string Kind => Colors is not null ? "RGBA" : Channel is not null ? "1 channel" : "Binary mask";

    public RasterFrame(Image2D<Color4b> colors, string details = "")
    {
        Colors = colors;
        Width = colors.Width;
        Height = colors.Height;
        Details = details;
    }

    public RasterFrame(Image2D<float> channel, string details = "")
    {
        Channel = channel;
        Width = channel.Width;
        Height = channel.Height;
        Details = details;
    }

    public RasterFrame(Image2D<bool> mask, string details = "")
    {
        Mask = mask;
        Width = mask.Width;
        Height = mask.Height;
        Details = details;
    }

    public static RasterFrame Load(string path)
    {
        using var texture = CpuTexture.Load(path);
        return new(texture.CopyImage());
    }

    public RasterFrame Clone(string? details = null) => Colors is not null ? new(Colors.Clone(), details ?? Details)
        : Channel is not null ? new(Channel.Clone(), details ?? Details) : new(Mask!.Clone(), details ?? Details);

    public Image2D<float> GetChannel(ChannelSource source = ChannelSource.Luminance)
    {
        if (Channel is not null)
        {
            return Channel.Clone();
        }
        if (Mask is not null)
        {
            return Mask.Map(static value => value ? 1f : 0f);
        }
        return Colors!.Map(color =>
        {
            if (source == ChannelSource.Alpha)
            {
                return color.A / 255f;
            }
            var rgb = CompositeOnWhite(color);
            return source switch
            {
                ChannelSource.Red => rgb.X,
                ChannelSource.Green => rgb.Y,
                ChannelSource.Blue => rgb.Z,
                ChannelSource.HsvHue => ColorSpaces2D.SrgbToHsv(rgb).X,
                ChannelSource.HsvSaturation => ColorSpaces2D.SrgbToHsv(rgb).Y,
                ChannelSource.HsvValue => ColorSpaces2D.SrgbToHsv(rgb).Z,
                ChannelSource.OklabLightness => ColorSpaces2D.SrgbToOklab(rgb).X,
                ChannelSource.OklabA => ColorSpaces2D.SrgbToOklab(rgb).Y + .5f,
                ChannelSource.OklabB => ColorSpaces2D.SrgbToOklab(rgb).Z + .5f,
                _ => Vector3.Dot(rgb, new(.2126f, .7152f, .0722f))
            };
        });
    }

    public Image2D<Color4b> DisplayPixels() => Colors ?? (Channel is not null
        ? Channel.Map(static value => new Color4b(Byte(value), Byte(value), Byte(value), (byte)255))
        : Mask!.Map(static value => value ? Color4b.White : Color4b.Black));

    public void SavePng(string path)
    {
        var image = DisplayPixels();
        using var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
        var data = bitmap.LockBits(new(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var row = new byte[checked(Width * 4)];
            for (var y = 0; y < Height; y++)
            {
                var pixels = image.GetRowSpan(y);
                for (var x = 0; x < Width; x++)
                {
                    var color = pixels[x];
                    row[x * 4] = color.B;
                    row[x * 4 + 1] = color.G;
                    row[x * 4 + 2] = color.R;
                    row[x * 4 + 3] = color.A;
                }
                Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, row.Length);
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }
        AtomicFile.Write(path, stream => bitmap.Save(stream, ImageFormat.Png));
    }

    internal static byte Byte(float value) => (byte)MathF.Round(Math.Clamp(value, 0, 1) * 255);

    internal static Vector3 CompositeOnWhite(Color4b color)
    {
        var alpha = color.A / 255f;
        return new Vector3(color.R, color.G, color.B) / 255f * alpha + new Vector3(1 - alpha);
    }
}
