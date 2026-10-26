using App2d.Core.Mathematics;
using App2d.Core.Validation;

namespace App2d.Core.Imaging;

/// <summary>Ordered outlines of binary foreground pixel cells in image coordinates.</summary>
public static class ImageContours2D
{
    /// <summary>
    /// Traces all boundary loops on integer pixel corners, including holes and image-border edges.
    /// Each returned path is implicitly closed (the first point is not repeated). Consecutive points
    /// are one pixel edge apart. Foreground stays on the right: outer loops have positive signed
    /// area in Y-down coordinates, hole loops negative. At ambiguous diagonal contacts, right turns
    /// separate corner-touching foreground cells. Loops can touch at a vertex; no smoothing or
    /// simplification is applied. This traces region perimeters, not centerlines of an edge stroke.
    /// </summary>
    public static Vector2i[][] TraceBoundaryLoops(this Image2D<bool> source)
    {
        ArgGuard.ThrowIfNull(source);
        var outgoing = new Dictionary<Vector2i, byte>();
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                if (!source[x, y])
                {
                    continue;
                }
                if (!source.GetValueOrDefault(x, y - 1)) Add(new(x, y), 0);
                if (!source.GetValueOrDefault(x + 1, y)) Add(new(x + 1, y), 1);
                if (!source.GetValueOrDefault(x, y + 1)) Add(new(x + 1, y + 1), 2);
                if (!source.GetValueOrDefault(x - 1, y)) Add(new(x, y + 1), 3);
            }
        }

        var visited = new HashSet<(Vector2i Point, int Direction)>();
        var loops = new List<Vector2i[]>();
        foreach (var (start, directions) in outgoing)
        {
            for (var direction = 0; direction < 4; direction++)
            {
                if ((directions & (1 << direction)) == 0 || visited.Contains((start, direction)))
                {
                    continue;
                }
                var points = new List<Vector2i>();
                var point = start;
                var heading = direction;
                do
                {
                    visited.Add((point, heading));
                    points.Add(point);
                    point += Step(heading);
                    heading = NextDirection(outgoing[point], heading);
                }
                while (point != start || heading != direction);
                loops.Add(points.ToArray());
            }
        }
        return loops.ToArray();

        void Add(Vector2i point, int direction) =>
            outgoing[point] = (byte)(outgoing.GetValueOrDefault(point) | (1 << direction));
    }

    private static int NextDirection(byte outgoing, int incoming)
    {
        var right = (incoming + 1) % 4;
        if ((outgoing & (1 << right)) != 0) return right;
        if ((outgoing & (1 << incoming)) != 0) return incoming;
        var left = (incoming + 3) % 4;
        return (outgoing & (1 << left)) != 0 ? left : (incoming + 2) % 4;
    }

    private static Vector2i Step(int direction) => direction switch
    {
        0 => new(1, 0),
        1 => new(0, 1),
        2 => new(-1, 0),
        _ => new(0, -1),
    };
}
