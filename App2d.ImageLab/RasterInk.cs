namespace App2d.ImageLab;

// Retain the actual scalar when sampling: the display's byte gray is only a swatch.
internal readonly record struct RasterInk(Color4b Color, float Scalar)
{
    public static RasterInk White => Gray(1);
    public static RasterInk Black => Gray(0);
    public static RasterInk Gray(float value) => new(new(RasterFrame.Byte(value), RasterFrame.Byte(value), RasterFrame.Byte(value)), value);
    public static RasterInk FromColor(Color4b color) => new(color, (.2126f * color.R + .7152f * color.G + .0722f * color.B) / 255f);

    public static RasterInk Sample(RasterFrame frame, int x, int y)
    {
        if ((uint)x >= (uint)frame.Width || (uint)y >= (uint)frame.Height)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }
        return SampleIndex(frame, y * frame.Width + x);
    }

    internal static RasterInk SampleIndex(RasterFrame frame, int index) => frame.Colors is not null
        ? FromColor(frame.Colors.AsSpan()[index]) : frame.Channel is not null
        ? Gray(frame.Channel.AsSpan()[index]) : Gray(frame.Mask!.AsSpan()[index] ? 1 : 0);

    public string Description(RasterFrame frame) => frame.Mask is not null ? (Scalar >= .5f ? "Foreground" : "Background")
        : frame.Channel is not null ? $"Value {Scalar:G6}" : $"#{Color.R:X2}{Color.G:X2}{Color.B:X2}{Color.A:X2}";

    public static RasterInk[] Palette(RasterFrame frame, int maximum = 24)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximum, 1);
        if (frame.Mask is not null)
        {
            return new[] { Black, White }.Take(maximum).ToArray();
        }
        var counts = new Dictionary<uint, (int Count, RasterInk Ink)>();
        for (var i = 0; i < frame.Width * frame.Height; i++)
        {
            var ink = SampleIndex(frame, i);
            var key = ink.Color.PackedValue;
            if (counts.TryGetValue(key, out var entry))
            {
                counts[key] = (entry.Count + 1, entry.Ink);
            }
            else
            {
                counts.Add(key, (1, ink));
            }
        }
        return counts.OrderByDescending(static pair => pair.Value.Count).ThenBy(static pair => pair.Key)
            .Take(maximum).Select(static pair => pair.Value.Ink).ToArray();
    }
}
