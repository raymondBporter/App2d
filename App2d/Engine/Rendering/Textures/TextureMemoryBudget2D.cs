namespace App2d.Engine.Rendering.Textures;

/// <summary>
/// Desktop texture residency targets. CPU atlas pixels and GPU resources are
/// tracked separately because Skia may retain both while a raster image is
/// available for context recovery.
/// </summary>
public static class TextureMemoryBudget2D
{
    public const long CompositeFrameCacheBytes = 256L * 1024L * 1024L;
    public const long GpuResourceCacheBytes = 384L * 1024L * 1024L;
}
