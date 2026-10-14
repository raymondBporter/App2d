using App2d.Core.IO;
using System.Drawing;
using System.Drawing.Imaging;

namespace App2d.Core.Characters.Spine;

/// <summary>Reads region images from modern or legacy Spine text atlases, restoring trim and packing rotation.</summary>
public sealed class SpineAtlas2D
{
    private sealed record Region(string Page, bool Premultiplied, Dictionary<string, string> Values);
    private readonly Dictionary<string, Region> _regions = new(StringComparer.Ordinal);
    private readonly string _root;
    public SpineAtlas2D(string path)
    {
        _root = Path.GetDirectoryName(Path.GetFullPath(path))!;
        string? page = null; Region? region = null; var pma = false; var newPage = true;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.Length == 0) { newPage = true; region = null; continue; }
            var colon = line.IndexOf(':');
            if (colon < 0)
            {
                if (newPage) { page = line; pma = false; region = null; newPage = false; }
                else
                {
                    region = new(page!, pma, new(StringComparer.Ordinal));
                    if (!_regions.TryAdd(line, region)) throw new InvalidDataException($"Duplicate atlas region '{line}'; indexed sequences are not supported.");
                }
            }
            else if (region is not null)
            {
                region.Values[line[..colon].Trim()] = line[(colon + 1)..].Trim();
            }
            else if (line[..colon].Trim() == "pma")
            {
                pma = line[(colon + 1)..].Trim() == "true";
            }
        }
    }

    public byte[] Extract(string name)
    {
        if (!_regions.TryGetValue(name, out var region)) throw new InvalidDataException($"Atlas has no region '{name}'.");
        int[] Values(string key, int[] fallback) => region.Values.TryGetValue(key, out var value)
            ? [.. value.Split(',').Select(s => int.Parse(s.Trim(), System.Globalization.CultureInfo.InvariantCulture))] : fallback;
        var xy = Values("xy", [0, 0]); var size = Values("size", [0, 0]);
        if (xy.Length != 2 || size.Length != 2) throw new InvalidDataException($"Invalid atlas position or size for '{name}'.");
        var bounds = Values("bounds", [xy[0], xy[1], size[0], size[1]]);
        if (bounds.Length != 4 || bounds[0] < 0 || bounds[1] < 0 || bounds[2] <= 0 || bounds[3] <= 0) throw new InvalidDataException($"Invalid atlas bounds for '{name}'.");
        var rotation = region.Values.GetValueOrDefault("rotate", "false") switch { "true" => 90, "false" => 0, var s => int.Parse(s) };
        if (rotation is not (0 or 90 or 180 or 270)) throw new InvalidDataException($"Unsupported atlas rotation {rotation}.");
        using var page = new Bitmap(FilePaths.ResolveUnderRoot(_root, region.Page));
        var rect = new Rectangle(bounds[0], bounds[1], rotation is 90 or 270 ? bounds[3] : bounds[2], rotation is 90 or 270 ? bounds[2] : bounds[3]);
        if (rect.Right > page.Width || rect.Bottom > page.Height) throw new InvalidDataException($"Atlas region '{name}' exceeds its page.");
        using var image = page.Clone(rect, PixelFormat.Format32bppArgb);
        image.RotateFlip(rotation switch { 90 => RotateFlipType.Rotate90FlipNone, 180 => RotateFlipType.Rotate180FlipNone, 270 => RotateFlipType.Rotate270FlipNone, _ => RotateFlipType.RotateNoneFlipNone });
        if (region.Premultiplied)
        {
            for (var y = 0; y < image.Height; y++)
            {
                for (var x = 0; x < image.Width; x++)
            {
                var c = image.GetPixel(x, y); if (c.A == 0) continue;
                image.SetPixel(x, y, Color.FromArgb(c.A, Math.Min(255, c.R * 255 / c.A), Math.Min(255, c.G * 255 / c.A), Math.Min(255, c.B * 255 / c.A)));
            }
            }
        }

        var orig = Values("orig", [image.Width, image.Height]); var offset = Values("offset", [0, 0]);
        if (orig.Length != 2 || offset.Length != 2) throw new InvalidDataException($"Invalid atlas original size or offset for '{name}'.");
        var offsets = Values("offsets", [offset[0], offset[1], orig[0], orig[1]]);
        if (offsets.Length != 4 || offsets[2] <= 0 || offsets[3] <= 0 || offsets[2] > 8192 || offsets[3] > 8192 || offsets[0] < 0 || offsets[1] < 0 || offsets[0] + image.Width > offsets[2] || offsets[1] + image.Height > offsets[3])
            throw new InvalidDataException($"Invalid atlas trim for '{name}'.");
        using var restored = new Bitmap(offsets[2], offsets[3], PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(restored)) graphics.DrawImageUnscaled(image, offsets[0], restored.Height - offsets[1] - image.Height);
        using var stream = new MemoryStream(); restored.Save(stream, ImageFormat.Png); return stream.ToArray();
    }
}
