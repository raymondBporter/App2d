namespace App2d.Engine.Tiles;

public readonly record struct TileCellRectangle2D(int X, int Y, int Width, int Height, TileKind2D Kind);

public static class TileRectangleMesher2D
{
    public delegate TileKind2D KindAt(int x, int y);

    /// <summary>
    /// Greedy rectangle merge in cell space: horizontal runs of one kind, grown
    /// vertically only for solid kinds (one-way surfaces stay one tile tall so
    /// their walkable tops are preserved).
    /// </summary>
    public static void Mesh(int width, int height, KindAt getKind, List<TileCellRectangle2D> results)
    {
        ArgGuard.ThrowIfNotPositive(width);
        ArgGuard.ThrowIfNotPositive(height);
        ArgGuard.ThrowIfNull(getKind);
        ArgGuard.ThrowIfNull(results);

        var consumed = new bool[width * height];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                var kind = getKind(x, y);
                if (!kind.IsCollidable() || consumed[index])
                    continue;

                var rectangleWidth = 1;
                while (x + rectangleWidth < width && !consumed[index + rectangleWidth] && getKind(x + rectangleWidth, y) == kind)
                {
                    rectangleWidth++;
                }

                var rectangleHeight = 1;
                while (kind.IsSolid() && y + rectangleHeight < height && IsUnconsumedRun(getKind, consumed, width, x, y + rectangleHeight, rectangleWidth, kind))
                {
                    rectangleHeight++;
                }

                for (var row = y; row < y + rectangleHeight; row++)
                    consumed.AsSpan(row * width + x, rectangleWidth).Fill(true);

                results.Add(new TileCellRectangle2D(x, y, rectangleWidth, rectangleHeight, kind));
            }
        }
    }

    private static bool IsUnconsumedRun(KindAt getKind, ReadOnlySpan<bool> consumed, int rowWidth, int x, int y, int width, TileKind2D kind)
    {
        var start = y * rowWidth + x;
        for (var offset = 0; offset < width; offset++)
        {
            if (consumed[start + offset] || getKind(x + offset, y) != kind)
                return false;
        }

        return true;
    }
}
