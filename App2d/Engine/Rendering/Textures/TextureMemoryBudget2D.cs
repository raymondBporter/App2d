namespace App2d.Engine.Rendering.Textures;

/// <summary>Desktop texture residency target for cached GPU resources.</summary>
public static class TextureMemoryBudget2D
{
    public const long GpuResourceCacheBytes = 384L * 1024L * 1024L;
}
