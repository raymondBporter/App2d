using App2d.Core.Imaging;
using System.Numerics;

namespace App2d.ImageLab;

internal sealed class RasterRegions
{
    public Image2D<int> Labels { get; }
    public int[] PixelCounts { get; }
    public int[] LargestFirst { get; }
    public int Count => PixelCounts.Length - 1;

    public RasterRegions(Image2D<bool> mask, bool eightConnected)
    {
        (Labels, PixelCounts) = mask.LabelComponents(eightConnected);
        LargestFirst = [.. Enumerable.Range(1, Count).OrderByDescending(id => PixelCounts[id]).ThenBy(static id => id)];
    }

    public RasterFrame Filter(int largestCount, int minimumPixels, bool largestOnly, bool eightConnected)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(largestCount, 1);
        var keep = new bool[PixelCounts.Length];
        if (largestOnly)
        {
            foreach (var id in LargestFirst.Take(largestCount))
            {
                keep[id] = true;
            }
        }
        else
        {
            for (var id = 1; id < keep.Length; id++)
            {
                keep[id] = PixelCounts[id] >= minimumPixels;
            }
        }
        var kept = keep.Count(static value => value);
        return new(Labels.Map(id => keep[id]), $"Kept {kept} of {Count} regions · {(eightConnected ? 8 : 4)}-neighbor");
    }

    public int PickRegion(Vector2 point, int radius, Image2D<bool> visibleMask)
    {
        if (!(point.X >= 0 && point.Y >= 0 && point.X < Labels.Width && point.Y < Labels.Height))
        {
            return 0;
        }
        var cx = (int)point.X;
        var cy = (int)point.Y;
        if (!visibleMask.Contains(cx, cy))
        {
            return 0;
        }
        if (visibleMask[cx, cy] && Labels[cx, cy] != 0)
        {
            return Labels[cx, cy];
        }
        var nearest = 0;
        var distance = (float)(radius * radius);
        for (var y = Math.Max(0, cy - radius); y <= Math.Min(Labels.Height - 1, cy + radius); y++)
        {
            for (var x = Math.Max(0, cx - radius); x <= Math.Min(Labels.Width - 1, cx + radius); x++)
            {
                var id = Labels[x, y];
                if (id == 0 || !visibleMask[x, y])
                {
                    continue;
                }
                var candidate = Vector2.DistanceSquared(point, new(x + .5f, y + .5f));
                if (candidate < distance || (candidate == distance && (nearest == 0 || id < nearest)))
                {
                    distance = candidate;
                    nearest = id;
                }
            }
        }
        return nearest;
    }
}
