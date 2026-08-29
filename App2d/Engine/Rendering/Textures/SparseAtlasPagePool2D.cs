namespace App2d.Engine.Rendering.Textures;

internal static class SparseAtlasPagePool2D
{
    private static readonly object Sync = new();
    private static readonly Dictionary<string, SharedPage> Pages = new(
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal);

    public static SparseAtlasPageLease2D Acquire(
        string colorPath,
        string depthPath,
        int expectedWidth,
        int expectedHeight)
    {
        var key = colorPath + '\0' + depthPath;
        lock (Sync)
        {
            if (Pages.TryGetValue(key, out var existing))
            {
                existing.ReferenceCount++;
                return new SparseAtlasPageLease2D(
                    key,
                    existing.Color,
                    existing.Depth,
                    Release);
            }

            var color = Texture2D.Load(colorPath);
            DepthAtlas2D? depth = null;
            try
            {
                depth = DepthAtlas2D.LoadR16Unorm(depthPath);
                if (color.Width != expectedWidth || color.Height != expectedHeight ||
                    depth.Width != expectedWidth || depth.Height != expectedHeight)
                {
                    throw new InvalidDataException(
                        $"Declared atlas size {expectedWidth}x{expectedHeight} does not " +
                        "match the color/depth data.");
                }
                Pages.Add(key, new SharedPage(color, depth, 1));
                return new SparseAtlasPageLease2D(key, color, depth, Release);
            }
            catch
            {
                color.Dispose();
                depth?.Dispose();
                throw;
            }
        }
    }

    private static void Release(string key)
    {
        lock (Sync)
        {
            if (!Pages.TryGetValue(key, out var page))
                return;
            page.ReferenceCount--;
            if (page.ReferenceCount > 0)
                return;
            Pages.Remove(key);
            page.Color.Dispose();
            page.Depth.Dispose();
        }
    }

    private sealed class SharedPage(
        Texture2D color,
        DepthAtlas2D depth,
        int referenceCount)
    {
        public Texture2D Color { get; } = color;
        public DepthAtlas2D Depth { get; } = depth;
        public int ReferenceCount { get; set; } = referenceCount;
    }
}

internal sealed class SparseAtlasPageLease2D(
    string key,
    Texture2D color,
    DepthAtlas2D depth,
    Action<string> release) : IDisposable
{
    private Action<string>? _release = release;

    public Texture2D Color { get; } = color;
    public DepthAtlas2D Depth { get; } = depth;

    public void Dispose()
    {
        Interlocked.Exchange(ref _release, null)?.Invoke(key);
        GC.SuppressFinalize(this);
    }
}
