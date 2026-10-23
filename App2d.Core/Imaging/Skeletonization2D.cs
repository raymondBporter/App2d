using App2d.Core.Validation;

namespace App2d.Core.Imaging;

public static class Skeletonization2D
{
    /// <summary>
    /// Returns a skeleton using Guo-Hall two-subiteration thinning until stable.
    /// True is foreground; pixels outside the image are background. The source is unchanged.
    /// </summary>
    /// <remarks>
    /// Z. Guo and R. W. Hall, "Parallel thinning with two-subiteration algorithms", 1989.
    /// https://doi.org/10.1145/62065.62074
    /// This is binary thinning, not a distance-based medial axis or an anatomical joint detector.
    /// </remarks>
    public static Image2D<bool> Skeletonize(this Image2D<bool> source)
    {
        ArgGuard.ThrowIfNull(source);
        var skeleton = source.Clone();
        if (skeleton.Count == 0)
        {
            return skeleton;
        }

        var removals = new bool[skeleton.Count];
        bool changed;
        do
        {
            changed = ThinPhase(skeleton, removals, secondPhase: false);
            changed |= ThinPhase(skeleton, removals, secondPhase: true);
        }
        while (changed);
        return skeleton;
    }

    private static bool ThinPhase(Image2D<bool> image, Span<bool> removals, bool secondPhase)
    {
        var pixels = image.AsSpan();
        var changed = false;
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var index = y * image.Width + x;
                if (!pixels[index])
                {
                    continue;
                }

                // Clockwise neighbors: NW N NE / W center E / SW S SE.
                var n = image.GetValueOrDefault(x, y - 1);
                var ne = image.GetValueOrDefault(x + 1, y - 1);
                var e = image.GetValueOrDefault(x + 1, y);
                var se = image.GetValueOrDefault(x + 1, y + 1);
                var s = image.GetValueOrDefault(x, y + 1);
                var sw = image.GetValueOrDefault(x - 1, y + 1);
                var w = image.GetValueOrDefault(x - 1, y);
                var nw = image.GetValueOrDefault(x - 1, y - 1);

                var crossings = AsInt(!n && (ne || e)) + AsInt(!e && (se || s))
                    + AsInt(!s && (sw || w)) + AsInt(!w && (nw || n));
                if (crossings != 1)
                {
                    continue;
                }

                var pairs1 = AsInt(nw || n) + AsInt(ne || e) + AsInt(se || s) + AsInt(sw || w);
                var pairs2 = AsInt(n || ne) + AsInt(e || se) + AsInt(s || sw) + AsInt(w || nw);
                var neighbors = Math.Min(pairs1, pairs2);
                var protectedPixel = secondPhase ? (n || ne || !se) && e : (s || sw || !nw) && w;
                if (neighbors is >= 2 and <= 3 && !protectedPixel)
                {
                    removals[index] = true;
                    changed = true;
                }
            }
        }

        // Decisions within a phase all read the same image. Deleting during the scan breaks connectivity.
        if (changed)
        {
            for (var i = 0; i < pixels.Length; i++)
            {
                if (removals[i])
                {
                    pixels[i] = false;
                    removals[i] = false;
                }
            }
        }
        return changed;
    }

    private static int AsInt(bool value) => value ? 1 : 0;
}
