using App2d.Core.Imaging;
using System.Numerics;

namespace App2d.ImageLab;

internal enum TransformKind { SingleChannel, Invert, Sobel, Canny, Threshold, Palette, FillHoles, RemoveSmallRegions, LargestRegion, Contours, Skeleton }
internal enum ChannelSource { Luminance, Alpha, Red, Green, Blue, HsvHue, HsvSaturation, HsvValue, OklabLightness, OklabA, OklabB }

internal sealed record RasterTransform(
    TransformKind Kind = TransformKind.SingleChannel,
    ChannelSource Channel = ChannelSource.Luminance,
    float Threshold = .1f,
    bool DarkForeground = false,
    int Colors = 8,
    int Iterations = 16,
    int MinimumPixels = 50,
    bool EightConnected = true,
    float WeakEdge = .01f,
    float StrongEdge = .03f,
    float Smoothing = 1.2f,
    int LargestRegions = 1)
{
    public static string ChannelName(ChannelSource channel) => channel switch
    {
        ChannelSource.HsvHue => "HSV hue (wraps)",
        ChannelSource.HsvSaturation => "HSV saturation",
        ChannelSource.HsvValue => "HSV value",
        ChannelSource.OklabLightness => "Oklab lightness",
        ChannelSource.OklabA => "Oklab a (+0.5)",
        ChannelSource.OklabB => "Oklab b (+0.5)",
        _ => channel.ToString()
    };

    public static string Name(TransformKind kind) => kind switch
    {
        TransformKind.SingleChannel => "Convert to 1 channel",
        TransformKind.Sobel => "Find edges (Sobel)",
        TransformKind.Canny => "Find connected edges (Canny)",
        TransformKind.Palette => "Reduce palette",
        TransformKind.FillHoles => "Fill holes",
        TransformKind.RemoveSmallRegions => "Remove small regions",
        TransformKind.LargestRegion => "Keep largest N regions",
        TransformKind.Contours => "Find contours (raster)",
        _ => kind.ToString()
    };

    public string Description => Kind switch
    {
        TransformKind.SingleChannel => $"1 channel · {ChannelName(Channel)}",
        TransformKind.Canny => $"Canny · {ChannelName(Channel)} · {WeakEdge:0.000}/{StrongEdge:0.000} · blur {Smoothing:0.0}",
        TransformKind.Threshold => $"Threshold · {(DarkForeground ? "below" : "at least")} {Threshold:0.000}",
        TransformKind.Palette => $"Palette · {Colors} colors, {Iterations} iterations",
        TransformKind.RemoveSmallRegions => $"Remove regions < {MinimumPixels} px · {(EightConnected ? 8 : 4)} connected",
        TransformKind.LargestRegion => $"Keep {LargestRegions} largest regions · {(EightConnected ? 8 : 4)} connected",
        _ => Name(Kind)
    };

    public string? UnavailableReason(RasterFrame frame) => Kind switch
    {
        TransformKind.FillHoles or TransformKind.RemoveSmallRegions or TransformKind.LargestRegion
            or TransformKind.Contours or TransformKind.Skeleton when frame.Mask is null => "Apply Threshold first to create a binary mask.",
        TransformKind.Palette when frame.Colors is null => "Palette reduction needs a color image. Select an earlier color step.",
        _ => null
    };

    public RasterFrame Apply(RasterFrame source)
    {
        if (UnavailableReason(source) is { } reason)
        {
            throw new InvalidOperationException(reason);
        }
        return Kind switch
        {
            TransformKind.SingleChannel => new(source.GetChannel(Channel)),
            TransformKind.Invert => Invert(source),
            TransformKind.Sobel => new(source.Colors is not null
                ? source.Colors.Map(static color => ColorSpaces2D.SrgbToOklab(RasterFrame.CompositeOnWhite(color))).SobelMagnitude()
                : source.GetChannel().Sobel().Map(static gradient => gradient.Length())),
            TransformKind.Canny => new(source.GetChannel(Channel).CannyEdges(WeakEdge, StrongEdge, Smoothing),
                "Thin edges · linked weak lines"),
            TransformKind.Threshold => new(source.GetChannel().Map(value => DarkForeground ? value < Threshold : value >= Threshold)),
            TransformKind.Palette => Quantize(source),
            TransformKind.FillHoles => new(source.Mask!.FillHoles()),
            TransformKind.RemoveSmallRegions or TransformKind.LargestRegion => FilterRegions(new(source.Mask!, EightConnected)),
            TransformKind.Contours => Contours(source.Mask!),
            TransformKind.Skeleton => new(source.Mask!.Skeletonize()),
            _ => throw new ArgumentOutOfRangeException(nameof(Kind))
        };
    }

    private static RasterFrame Invert(RasterFrame source) => source.Colors is not null
        ? new(source.Colors.Map(static c => new Color4b((byte)(255 - c.R), (byte)(255 - c.G), (byte)(255 - c.B), c.A)))
        : source.Channel is not null ? new(source.Channel.Map(static value => 1 - value))
        : new(source.Mask!.Map(static value => !value));

    private RasterFrame Quantize(RasterFrame source)
    {
        // Quantize visible samples only; hidden RGB must not move palette centers.
        var input = source.Colors!;
        var visible = input.AsSpan().ToArray().Where(static color => color.A != 0).ToArray();
        if (visible.Length == 0)
        {
            return new(input.Clone(), "0 visible colors");
        }
        var lab = new Image2D<Color4b>(visible.Length, 1, visible)
            .Map(static c => ColorSpaces2D.SrgbToOklab(new Vector3(c.R, c.G, c.B) / 255f));
        var (palette, indices) = lab.QuantizePalette(Colors, Iterations);
        var displayPalette = palette.Select(static color => ColorSpaces2D.OklabToSrgb(color)).ToArray();
        var result = input.Clone();
        var output = result.AsSpan();
        var labels = indices.AsSpan();
        var index = 0;
        for (var i = 0; i < output.Length; i++)
        {
            if (output[i].A == 0)
            {
                continue;
            }
            var rgb = displayPalette[labels[index++]];
            output[i] = new(RasterFrame.Byte(rgb.X), RasterFrame.Byte(rgb.Y), RasterFrame.Byte(rgb.Z), output[i].A);
        }
        return new(result, $"{palette.Length} visible palette colors · alpha preserved");
    }

    internal RasterFrame FilterRegions(RasterRegions regions) =>
        regions.Filter(LargestRegions, MinimumPixels, Kind == TransformKind.LargestRegion, EightConnected);

    private static RasterFrame Contours(Image2D<bool> mask)
    {
        var loops = mask.TraceBoundaryLoops();
        var result = new Image2D<bool>(mask.Width, mask.Height);
        foreach (var loop in loops)
        {
            for (var i = 0; i < loop.Length; i++)
            {
                var point = loop[i];
                var next = loop[(i + 1) % loop.Length];
                // Each edge's foreground cell is on its right in Y-down coordinates.
                var x = point.X - (next.Y > point.Y || next.X < point.X ? 1 : 0);
                var y = point.Y - (next.Y < point.Y || next.X < point.X ? 1 : 0);
                result[x, y] = true;
            }
        }
        return new(result, $"{loops.Length} boundary loops · holes preserved · raster output");
    }
}
