using App2d.Core.Validation;

namespace App2d.Core.Imaging;

/// <summary>Connected pixel groups and enclosed background in binary images.</summary>
public static class BinaryImageRegions2D
{
    /// <summary>
    /// Labels foreground groups with positive IDs in row-major discovery order. Zero is background;
    /// PixelCounts[id] counts pixels with that label, including background at index zero.
    /// Eight-connectivity includes diagonal neighbors; false uses four cardinal neighbors.
    /// Labels are groups of pixels, not ordered paths. The source is unchanged.
    /// </summary>
    public static (Image2D<int> Labels, int[] PixelCounts) LabelComponents(this Image2D<bool> source, bool eightConnected = true)
    {
        ArgGuard.ThrowIfNull(source);
        var labels = new Image2D<int>(source.Width, source.Height);
        var counts = new List<int> { 0 };
        var pending = new Queue<int>();
        var pixels = source.AsSpan();
        var output = labels.AsSpan();
        for (var start = 0; start < pixels.Length; start++)
        {
            if (!pixels[start])
            {
                counts[0]++;
                continue;
            }
            if (output[start] != 0)
            {
                continue;
            }

            var label = counts.Count;
            var count = 0;
            output[start] = label;
            pending.Enqueue(start);
            while (pending.TryDequeue(out var current))
            {
                count++;
                var x = current % source.Width;
                var y = current / source.Width;
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if ((dx == 0 && dy == 0) || (!eightConnected && dx != 0 && dy != 0))
                        {
                            continue;
                        }
                        var nx = x + dx;
                        var ny = y + dy;
                        if (!source.Contains(nx, ny))
                        {
                            continue;
                        }
                        var index = ny * source.Width + nx;
                        if (pixels[index] && output[index] == 0)
                        {
                            output[index] = label;
                            pending.Enqueue(index);
                        }
                    }
                }
            }
            counts.Add(count);
        }
        return (labels, counts.ToArray());
    }

    /// <summary>
    /// Fills background that cannot reach an image border by four-connected background steps.
    /// Outside is background. Diagonal-only foreground contacts block leakage between their corners.
    /// On an edge image this fills closed enclosures; gaps can let the exterior enter the object.
    /// This intentionally fills holes and does not repair gaps or distinguish meaningful holes from noise.
    /// </summary>
    public static Image2D<bool> FillHoles(this Image2D<bool> source)
    {
        ArgGuard.ThrowIfNull(source);
        var background = source.Map(static foreground => !foreground).LabelComponents(eightConnected: false);
        var exterior = new bool[background.PixelCounts.Length];
        for (var x = 0; x < source.Width && source.Height > 0; x++)
        {
            exterior[background.Labels[x, 0]] = true;
            exterior[background.Labels[x, source.Height - 1]] = true;
        }
        for (var y = 0; y < source.Height && source.Width > 0; y++)
        {
            exterior[background.Labels[0, y]] = true;
            exterior[background.Labels[source.Width - 1, y]] = true;
        }
        var result = source.Clone();
        var filled = result.AsSpan();
        var labels = background.Labels.AsSpan();
        for (var i = 0; i < filled.Length; i++)
        {
            filled[i] |= !exterior[labels[i]];
        }
        return result;
    }
}
